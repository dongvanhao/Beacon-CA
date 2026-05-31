using Beacon.Application.Features.Authorization.Dtos;
using Beacon.Application.Mappings.Authorization;
using Beacon.Domain.Entities.Identity;
using Beacon.Domain.IRepository.Identity;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Authorization.Permissions.Commands.CreatePermission;

public class CreatePermissionCommandHandler(
    IPermissionRepository permissionRepository,
    PermissionMapper mapper) : IRequestHandler<CreatePermissionCommand, Result<PermissionDto>>
{
    public async Task<Result<PermissionDto>> Handle(CreatePermissionCommand command, CancellationToken ct)
    {
        var name = command.Request.Name.Trim();
        if (await permissionRepository.ExistsByNameAsync(name, ct: ct))
            return Result<PermissionDto>.Failure(
                Error.Conflict(ErrorCodes.Authorization.PERMISSION_ALREADY_EXISTS, "Permission đã tồn tại."));

        var permission = Permission.Create(
            name,
            Normalize(command.Request.Description),
            Normalize(command.Request.Group));

        await permissionRepository.AddAsync(permission, ct);
        await permissionRepository.SaveChangesAsync(ct);

        return Result<PermissionDto>.Success(mapper.ToDto(permission));
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
