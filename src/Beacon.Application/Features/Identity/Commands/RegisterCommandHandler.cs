using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Identity.Dtos;
using Beacon.Domain.Entities.Identity;
using Beacon.Domain.IRepository;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Identity.Commands;

public class RegisterCommandHandler(
    IUserRepository userRepository,
    IJwtService jwtService) : IRequestHandler<RegisterCommand, Result<AuthResponse>>
{
    public async Task<Result<AuthResponse>> Handle(RegisterCommand command, CancellationToken ct)
    {
        var req = command.Request;

        // 1. Check email conflict
        if (await userRepository.ExistsByEmailAsync(req.Email, ct))
            return Result<AuthResponse>.Failure(
                Error.Conflict(ErrorCodes.Identity.EMAIL_ALREADY_EXISTS, "Email is already registered."));

        // 2. Hash password
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(req.Password);

        // 3. Create user
        var user = User.Create(req.Email, passwordHash, req.FullName, req.PhoneNumber);
        await userRepository.AddAsync(user, ct);

        // 4. Generate tokens
        var (accessToken, accessTokenExpiresAt) = jwtService.GenerateAccessToken(user);
        var (refreshTokenValue, refreshTokenExpiresAt) = jwtService.GenerateRefreshToken();
        var refreshToken = RefreshToken.Create(
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
