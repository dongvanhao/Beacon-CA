using Beacon.Application.Features.Authorization.Dtos;
using Beacon.Application.Mappings.Authorization;
using Beacon.Domain.IRepository.Identity;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Authorization.Permissions.Commands.UpdatePermission;

public class UpdatePermissionCommandHandler(
    IPermissionRepository permissionRepository,
    PermissionMapper mapper) : IRequestHandler<UpdatePermissionCommand, Result<PermissionDto>>
{
    public async Task<Result<PermissionDto>> Handle(UpdatePermissionCommand command, CancellationToken ct)
    {
        var permission = await permissionRepository.GetByIdAsync(command.Id, ct);
        if (permission is null)
            return Result<PermissionDto>.Failure(
                Error.NotFound(ErrorCodes.Authorization.PERMISSION_NOT_FOUND, "Không tìm thấy permission."));

        var name = command.Request.Name.Trim();
        if (await permissionRepository.ExistsByNameAsync(name, permission.Id, ct))
            return Result<PermissionDto>.Failure(
                Error.Conflict(ErrorCodes.Authorization.PERMISSION_ALREADY_EXISTS, "Permission đã tồn tại."));

        permission.Update(
            name,
            Normalize(command.Request.Description),
            Normalize(command.Request.Group));

        await permissionRepository.SaveChangesAsync(ct);
        return Result<PermissionDto>.Success(mapper.ToDto(permission));
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
