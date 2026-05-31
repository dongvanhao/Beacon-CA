using Beacon.Domain.IRepository.Identity;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Authorization.Permissions.Commands.DeletePermission;

public class DeletePermissionCommandHandler(
    IPermissionRepository permissionRepository) : IRequestHandler<DeletePermissionCommand, Result>
{
    public async Task<Result> Handle(DeletePermissionCommand command, CancellationToken ct)
    {
        var permission = await permissionRepository.GetByIdAsync(command.Id, ct);
        if (permission is null)
            return Result.Failure(
                Error.NotFound(ErrorCodes.Authorization.PERMISSION_NOT_FOUND, "Không tìm thấy permission."));

        if (await permissionRepository.IsAssignedToAnyRoleAsync(permission.Id, ct))
            return Result.Failure(
                Error.Conflict(ErrorCodes.Authorization.PERMISSION_IN_USE, "Permission đang được gắn với role."));

        permissionRepository.Remove(permission);
        await permissionRepository.SaveChangesAsync(ct);

        return Result.Success();
    }
}
