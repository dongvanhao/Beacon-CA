using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Identity.Dtos;
using Beacon.Domain.Entities.Identity;
using Beacon.Domain.IRepository;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Identity.Commands;

public class RefreshTokenCommandHandler(
    IUserRepository userRepository,
    IJwtService jwtService) : IRequestHandler<RefreshTokenCommand, Result<AuthResponse>>
{
    public async Task<Result<AuthResponse>> Handle(RefreshTokenCommand command, CancellationToken ct)
    {
        // 1. Find the active refresh token
        var existingToken = await userRepository.GetActiveRefreshTokenAsync(command.RefreshToken, ct);
        if (existingToken is null)
            return Result<AuthResponse>.Failure(
                Error.Unauthorized(ErrorCodes.Identity.TOKEN_INVALID, "Refresh token is invalid or expired."));

        // 2. Load the associated user
        var user = await userRepository.GetByIdAsync(existingToken.UserId, ct);
        if (user is null)
            return Result<AuthResponse>.Failure(
                Error.NotFound(ErrorCodes.Identity.USER_NOT_FOUND, "User not found."));

        // 3. Check account status
        if (!user.IsActive)
            return Result<AuthResponse>.Failure(
                Error.Unauthorized(ErrorCodes.Identity.ACCOUNT_INACTIVE, "Account is inactive."));

        // 4. Revoke old refresh token
        var deviceId = existingToken.UserDeviceId ?? Guid.Empty;
        existingToken.Revoke();

        // 5. Generate new tokens
        var (accessToken, accessTokenExpiresAt) = jwtService.GenerateAccessToken(user, deviceId);
        var (newRefreshTokenValue, newRefreshTokenExpiresAt) = jwtService.GenerateRefreshToken();

        var newRefreshToken = RefreshToken.Create(
            userId: user.Id,
            token: newRefreshTokenValue,
            expiresAtUtc: newRefreshTokenExpiresAt,
            userDeviceId: deviceId == Guid.Empty ? null : deviceId);

        await userRepository.AddRefreshTokenAsync(newRefreshToken, ct);
        await userRepository.SaveChangesAsync(ct);

        return Result<AuthResponse>.Success(new AuthResponse
        {
            UserId = user.Id,
            Username = user.Username,
            FullName = user.FullName,
            AccessToken = accessToken,
            RefreshToken = newRefreshTokenValue,
            AccessTokenExpiresAt = accessTokenExpiresAt
        });
    }
}
