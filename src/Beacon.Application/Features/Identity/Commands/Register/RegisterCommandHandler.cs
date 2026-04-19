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

public class RegisterCommandHandler(
    IUserRepository userRepository,
    IUserDeviceRepository deviceRepository,
    IJwtService jwtService,
    UserAuthMapper authMapper) : IRequestHandler<RegisterCommand, Result<AuthResponse>>
{
    public async Task<Result<AuthResponse>> Handle(RegisterCommand command, CancellationToken ct)
    {
        var req = command.Request;

        // 1. Check username conflict
        if (await userRepository.ExistsByUsernameAsync(req.Username, ct))
            return Result<AuthResponse>.Failure(
                Error.Conflict(ErrorCodes.Identity.USERNAME_ALREADY_EXISTS, "Username is already taken."));

        // 2. Check email conflict
        if (await userRepository.ExistsByEmailAsync(req.Email, ct))
            return Result<AuthResponse>.Failure(
                Error.Conflict(ErrorCodes.Identity.EMAIL_ALREADY_EXISTS, "Email is already taken."));

        // 3. Check phone conflict (only when supplied)
        if (!string.IsNullOrWhiteSpace(req.PhoneNumber)
            && await userRepository.ExistsByPhoneAsync(req.PhoneNumber, ct))
            return Result<AuthResponse>.Failure(
                Error.Conflict(ErrorCodes.Identity.PHONE_ALREADY_EXISTS, "Phone number is already taken."));

        // 4. Hash password
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(req.Password);

        // 5. Create user
        var user = User.Create(
            username: req.Username,
            email: req.Email,
            passwordHash: passwordHash,
            familyName: req.FamilyName,
            givenName: req.GivenName,
            phoneNumber: req.PhoneNumber);
        await userRepository.AddAsync(user, ct);

        // 6. Auto-detect device from User-Agent header
        var (platform, deviceName) = ParseUserAgent(command.UserAgent);
        var device = UserDevice.Create(user.Id, platform, deviceName, Guid.NewGuid().ToString());
        await deviceRepository.AddAsync(device, ct);

        // 7. Save user + device to get their Ids
        await userRepository.SaveChangesAsync(ct);

        // 8. Generate tokens — embed DeviceId in access token
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
