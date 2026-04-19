using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Identity.Dtos;
using Beacon.Application.Mappings.Identity;
using Beacon.Domain.Entities.Identity;
using Beacon.Domain.Enums.Identity;
using Beacon.Domain.IRepository;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Identity.Commands;

public class LoginCommandHandler(
    IUserRepository userRepository,
    IUserDeviceRepository deviceRepository,
    IJwtService jwtService,
    UserAuthMapper authMapper) : IRequestHandler<LoginCommand, Result<AuthResponse>>
{
    public async Task<Result<AuthResponse>> Handle(LoginCommand command, CancellationToken ct)
    {
        var req = command.Request;

        // 1. Find user
        var user = await userRepository.GetByUsernameAsync(req.Username, ct);
        if (user is null)
            return Result<AuthResponse>.Failure(
                Error.Unauthorized(ErrorCodes.Identity.INVALID_CREDENTIALS, "Invalid username or password."));

        // 2. Check account status
        if (!user.IsActive)
            return Result<AuthResponse>.Failure(
                Error.Unauthorized(ErrorCodes.Identity.ACCOUNT_INACTIVE, "Account is inactive."));

        // 3. Verify password
        if (!BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return Result<AuthResponse>.Failure(
                Error.Unauthorized(ErrorCodes.Identity.INVALID_CREDENTIALS, "Invalid username or password."));

        // 4. Record login timestamp
        user.RecordLogin();

        // 5. Revoke all active refresh tokens (enforce single-device login)
        var activeTokens = await userRepository.GetActiveRefreshTokensByUserIdAsync(user.Id, ct);
        foreach (var t in activeTokens)
            t.Revoke();

        // 6. Auto-detect device from User-Agent header — client không cần gửi device info
        var (platform, deviceName) = ParseUserAgent(command.UserAgent);
        var device = UserDevice.Create(user.Id, platform, deviceName, Guid.NewGuid().ToString());
        await deviceRepository.AddAsync(device, ct);

        // 7. Save device first to get its Id
        await userRepository.SaveChangesAsync(ct);

        // 8. Generate tokens — embed DeviceId in access token so /devices/register can identify session
        var (accessToken, accessTokenExpiresAt) = jwtService.GenerateAccessToken(user, device.Id);
        var (refreshTokenValue, refreshTokenExpiresAt) = jwtService.GenerateRefreshToken();

        var refreshToken = RefreshToken.Create(
            userId: user.Id,
            token: refreshTokenValue,
            expiresAtUtc: refreshTokenExpiresAt,
            userDeviceId: device.Id);

        await userRepository.AddRefreshTokenAsync(refreshToken, ct);
        await userRepository.SaveChangesAsync(ct);

        return Result<AuthResponse>.Success(
            authMapper.ToAuthResponse(user, accessToken, refreshTokenValue, accessTokenExpiresAt));
    }

    // Đọc User-Agent header để tự nhận diện thiết bị — client không cần khai báo
    private static (DevicePlatform Platform, string DeviceName) ParseUserAgent(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
            return (DevicePlatform.Unknown, "Unknown Device");

        if (userAgent.Contains("Android"))
            return (DevicePlatform.Android, "Android Device");

        if (userAgent.Contains("iPhone") || userAgent.Contains("iPad"))
            return (DevicePlatform.iOS, "iOS Device");

        if (userAgent.Contains("Windows") || userAgent.Contains("Macintosh") || userAgent.Contains("Linux"))
            return (DevicePlatform.Web, "Web Browser");

        return (DevicePlatform.Unknown, "Unknown Device");
    }
}
