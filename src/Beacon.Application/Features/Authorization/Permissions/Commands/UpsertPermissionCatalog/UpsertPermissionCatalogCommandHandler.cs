using Beacon.Application.Features.Authorization.Dtos;
using Beacon.Application.Mappings.Authorization;
using Beacon.Domain.Entities.Identity;
using Beacon.Domain.IRepository.Identity;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Authorization.Permissions.Commands.UpsertPermissionCatalog;

public class UpsertPermissionCatalogCommandHandler(
    IPermissionRepository permissionRepository,
    PermissionMapper mapper) : IRequestHandler<UpsertPermissionCatalogCommand, Result<UpsertPermissionCatalogResultDto>>
{
    public async Task<Result<UpsertPermissionCatalogResultDto>> Handle(
        UpsertPermissionCatalogCommand command,
        CancellationToken ct)
    {
        var created = 0;
        var updated = 0;
        var unchanged = 0;

        foreach (var definition in PermissionCodes.All)
        {
            var permission = await permissionRepository.GetByNameAsync(definition.Name, ct);
            if (permission is null)
            {
                permission = Permission.Create(
                    definition.Name,
                    definition.Description,
                    definition.Group);

                await permissionRepository.AddAsync(permission, ct);
                created++;
                continue;
            }

            if (permission.Description == definition.Description && permission.Group == definition.Group)
            {
                unchanged++;
                continue;
            }

            permission.Update(definition.Name, definition.Description, definition.Group);
            updated++;
        }

        await permissionRepository.SaveChangesAsync(ct);

        var permissionNames = PermissionCodes.All.Select(x => x.Name).ToArray();
        var permissions = await permissionRepository.GetByNamesAsync(permissionNames, ct);

        return Result<UpsertPermissionCatalogResultDto>.Success(new UpsertPermissionCatalogResultDto
        {
            Total = PermissionCodes.All.Count,
            Created = created,
            Updated = updated,
            Unchanged = unchanged,
            Permissions = permissions.Select(mapper.ToDto).ToList()
        });
    }
}
