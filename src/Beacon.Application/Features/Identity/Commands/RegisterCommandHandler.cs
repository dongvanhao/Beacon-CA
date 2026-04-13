using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Identity.Dtos;
using Beacon.Domain.Entities.Identity;
using Beacon.Domain.Enums.Identity;
using Beacon.Domain.IRepository;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Identity.Commands;

public class RegisterCommandHandler(
    IUserRepository userRepository,
    IUserDeviceRepository deviceRepository,
    IJwtService jwtService) : IRequestHandler<RegisterCommand, Result<AuthResponse>>
{
    public async Task<Result<AuthResponse>> Handle(RegisterCommand command, CancellationToken ct)
    {
        var req = command.Request;

        // 1. Check username conflict
        if (await userRepository.ExistsByUsernameAsync(req.Username, ct))
            return Result<AuthResponse>.Failure(
                Error.Conflict(ErrorCodes.Identity.USERNAME_ALREADY_EXISTS, "Username is already taken."));

        // 2. Hash password
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(req.Password);

        // 3. Create user
        var user = User.Create(req.Username, passwordHash, req.FullName, req.PhoneNumber);
        await userRepository.AddAsync(user, ct);

        // 4. Auto-detect device from User-Agent header
        var (platform, deviceName) = ParseUserAgent(command.UserAgent);
        var device = UserDevice.Create(user.Id, platform, deviceName, Guid.NewGuid().ToString());
        await deviceRepository.AddAsync(device, ct);

        // 5. Save user + device to get their Ids
        await userRepository.SaveChangesAsync(ct);

        // 6. Generate tokens — embed DeviceId in access token
        var (accessToken, accessTokenExpiresAt) = jwtService.GenerateAccessToken(user, device.Id);
        var (refreshTokenValue, refreshTokenExpiresAt) = jwtService.GenerateRefreshToken();
        var refreshToken = RefreshToken.Create(
            userId: user.Id,
            token: refreshTokenValue,
            expiresAtUtc: refreshTokenExpiresAt,
            userDeviceId: device.Id);

        await userRepository.AddRefreshTokenAsync(refreshToken, ct);
        await userRepository.SaveChangesAsync(ct);

        return Result<AuthResponse>.Success(new AuthResponse
        {
            UserId = user.Id,
            Username = user.Username,
            FullName = user.FullName,
            AccessToken = accessToken,
            RefreshToken = refreshTokenValue,
            AccessTokenExpiresAt = accessTokenExpiresAt
        });
    }

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
