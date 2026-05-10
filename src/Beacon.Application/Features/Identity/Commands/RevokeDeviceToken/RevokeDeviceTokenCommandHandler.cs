using Beacon.Domain.IRepository.Identity;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Identity.Commands.RevokeDeviceToken;

public class RevokeDeviceTokenCommandHandler(
    IUserDeviceTokenRepository tokenRepo)
    : IRequestHandler<RevokeDeviceTokenCommand, Result>
{
    public async Task<Result> Handle(RevokeDeviceTokenCommand command, CancellationToken ct)
    {
        var token = await tokenRepo.GetByTokenAsync(command.Token, ct);
        if (token is null)
            return Result.Success();

        token.Revoke();
        await tokenRepo.SaveChangesAsync(ct);
        return Result.Success();
    }
}
