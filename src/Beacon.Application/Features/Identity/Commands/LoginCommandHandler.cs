using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Identity.Dtos;
using Beacon.Domain.IRepository;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Identity.Commands;

public class LoginCommandHandler(
    IUserRepository userRepository,
    IJwtService jwtService) : IRequestHandler<LoginCommand, Result<AuthResponse>>
{
    public async Task<Result<AuthResponse>> Handle(LoginCommand command, CancellationToken ct)
    {
        var req = command.Request;

        // 1. Find user
        var user = await userRepository.GetByEmailAsync(req.Email, ct);
        if (user is null)
            return Result<AuthResponse>.Failure(
                Error.Unauthorized(ErrorCodes.Identity.INVALID_CREDENTIALS, "Invalid email or password."));

        // 2. Check account status
        if (!user.IsActive)
            return Result<AuthResponse>.Failure(
                Error.Unauthorized(ErrorCodes.Identity.ACCOUNT_INACTIVE, "Account is inactive."));

        // 3. Verify password
        if (!BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return Result<AuthResponse>.Failure(
                Error.Unauthorized(ErrorCodes.Identity.INVALID_CREDENTIALS, "Invalid email or password."));

        // 4. Record login timestamp
        user.RecordLogin();

        // 5. Generate tokens
        var (accessToken, accessTokenExpiresAt) = jwtService.GenerateAccessToken(user);
        var (refreshTokenValue, refreshTokenExpiresAt) = jwtService.GenerateRefreshToken();
        var refreshToken = Beacon.Domain.Entities.Identity.RefreshToken.Create(
            userId: user.Id,
            token: refreshTokenValue,
            expiresAtUtc: refreshTokenExpiresAt);

        await userRepository.AddRefreshTokenAsync(refreshToken, ct);
        await userRepository.SaveChangesAsync(ct);

        return Result<AuthResponse>.Success(new AuthResponse
        {
            UserId = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            AccessToken = accessToken,
            RefreshToken = refreshTokenValue,
            AccessTokenExpiresAt = accessTokenExpiresAt
        });
    }
}
