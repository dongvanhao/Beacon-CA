using Beacon.Domain.IRepository;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Identity.Commands;

public class LogoutAdminCommandHandler(IAdminRepository adminRepository) : IRequestHandler<LogoutAdminCommand, Result>
{
    public async Task<Result> Handle(LogoutAdminCommand command, CancellationToken ct)
    {
        var token = await adminRepository.GetActiveRefreshTokenAsync(command.RefreshToken, ct);
        if (token is null)
            return Result.Failure(
                Error.NotFound(ErrorCodes.Identity.ADMIN_TOKEN_INVALID, "Refresh token not found or already revoked."));

        token.Revoke();
        await adminRepository.SaveChangesAsync(ct);

        return Result.Success();
    }
}
