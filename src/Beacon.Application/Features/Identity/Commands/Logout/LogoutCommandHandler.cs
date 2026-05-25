using Beacon.Domain.IRepository;
using Beacon.Domain.IRepository.Identity;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Identity.Commands;

public class LogoutCommandHandler(
    IUserRepository userRepository,
    IUserDeviceTokenRepository deviceTokenRepository) : IRequestHandler<LogoutCommand, Result>
{
    public async Task<Result> Handle(LogoutCommand command, CancellationToken ct)
    {
        var token = await userRepository.GetActiveRefreshTokenAsync(command.RefreshToken, ct);
        if (token is null)
            return Result.Failure(
                Error.NotFound(ErrorCodes.Identity.TOKEN_INVALID, "Refresh token not found or already revoked."));

        token.Revoke();

        var deviceTokens = await deviceTokenRepository.GetActiveByUserIdAsync(token.UserId, ct);
        foreach (var deviceToken in deviceTokens)
            deviceToken.Revoke();

        await userRepository.SaveChangesAsync(ct);

        return Result.Success();
    }
}
