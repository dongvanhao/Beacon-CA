using Beacon.Application.Features.Authorization.Dtos;
using Beacon.Application.Mappings.Authorization;
using Beacon.Domain.IRepository.Identity;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Authorization.Roles.Commands.UpdateRole;

public class UpdateRoleCommandHandler(
    IRoleRepository roleRepository,
    RoleMapper mapper) : IRequestHandler<UpdateRoleCommand, Result<RoleDto>>
{
    public async Task<Result<RoleDto>> Handle(UpdateRoleCommand command, CancellationToken ct)
    {
        var role = await roleRepository.GetByIdWithPermissionsAsync(command.Id, ct);
        if (role is null)
            return Result<RoleDto>.Failure(
                Error.NotFound(ErrorCodes.Authorization.ROLE_NOT_FOUND, "Không tìm thấy role."));

        var name = command.Request.Name.Trim();
        if (await roleRepository.ExistsByNameAsync(name, role.Id, ct))
            return Result<RoleDto>.Failure(
                Error.Conflict(ErrorCodes.Authorization.ROLE_ALREADY_EXISTS, "Role đã tồn tại."));

        role.Update(name, Normalize(command.Request.Description), command.Request.IsActive);

        await roleRepository.SaveChangesAsync(ct);

        var updatedRole = await roleRepository.GetByIdWithPermissionsNoTrackingAsync(role.Id, ct);
        return Result<RoleDto>.Success(mapper.ToDto(updatedRole!));
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
