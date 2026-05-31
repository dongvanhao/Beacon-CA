using Beacon.Domain.IRepository.Identity;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Authorization.Roles.Commands.DeleteRole;

public class DeleteRoleCommandHandler(
    IRoleRepository roleRepository) : IRequestHandler<DeleteRoleCommand, Result>
{
    public async Task<Result> Handle(DeleteRoleCommand command, CancellationToken ct)
    {
        var role = await roleRepository.GetByIdWithPermissionsAsync(command.Id, ct);
        if (role is null)
            return Result.Failure(
                Error.NotFound(ErrorCodes.Authorization.ROLE_NOT_FOUND, "Không tìm thấy role."));

        if (await roleRepository.IsAssignedToAnyAdminAsync(role.Id, ct))
            return Result.Failure(
                Error.Conflict(ErrorCodes.Authorization.ROLE_IN_USE, "Role đang được gắn với admin."));

        roleRepository.Remove(role);
        await roleRepository.SaveChangesAsync(ct);

        return Result.Success();
    }
}
