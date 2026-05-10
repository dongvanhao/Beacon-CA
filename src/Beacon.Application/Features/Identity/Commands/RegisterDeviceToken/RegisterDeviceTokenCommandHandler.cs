using Beacon.Domain.Entities.Identity;
using Beacon.Domain.IRepository.Identity;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Identity.Commands.RegisterDeviceToken;

public class RegisterDeviceTokenCommandHandler(
    IUserDeviceTokenRepository tokenRepo)
    : IRequestHandler<RegisterDeviceTokenCommand, Result>
{
    public async Task<Result> Handle(RegisterDeviceTokenCommand command, CancellationToken ct)
    {
        var existing = await tokenRepo.GetByTokenAsync(command.Token, ct);

        if (existing is null)
        {
            var token = UserDeviceToken.Create(
                command.UserId, command.Token, command.Platform,
                command.DeviceId, command.DeviceName, command.AppVersion);
            await tokenRepo.AddAsync(token, ct);
        }
        else if (existing.UserId == command.UserId)
        {
            existing.RecordUsage();
        }
        else
        {
            existing.UpdateOwner(command.UserId);
            existing.RecordUsage();
        }

        await tokenRepo.SaveChangesAsync(ct);
        return Result.Success();
    }
}
