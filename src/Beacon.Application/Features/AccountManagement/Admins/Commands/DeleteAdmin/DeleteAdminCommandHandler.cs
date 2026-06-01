using Beacon.Domain.IRepository;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.AccountManagement.Admins.Commands.DeleteAdmin;

public class DeleteAdminCommandHandler(IAdminRepository adminRepository)
    : IRequestHandler<DeleteAdminCommand, Result>
{
    public async Task<Result> Handle(DeleteAdminCommand command, CancellationToken ct)
    {
        var admin = await adminRepository.GetByIdAsync(command.AdminId, ct);
        if (admin is null)
            return Result.Failure(
                Error.NotFound(ErrorCodes.Identity.ADMIN_NOT_FOUND, "Khong tim thay admin."));

        admin.Deactivate();
        await adminRepository.SaveChangesAsync(ct);

        return Result.Success();
    }
}
