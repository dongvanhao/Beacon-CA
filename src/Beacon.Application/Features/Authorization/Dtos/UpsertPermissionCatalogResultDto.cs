namespace Beacon.Application.Features.Authorization.Dtos;

public class UpsertPermissionCatalogResultDto
{
    public int Total { get; set; }
    public int Created { get; set; }
    public int Updated { get; set; }
    public int Unchanged { get; set; }
    public IEnumerable<PermissionDto> Permissions { get; set; } = [];
}
