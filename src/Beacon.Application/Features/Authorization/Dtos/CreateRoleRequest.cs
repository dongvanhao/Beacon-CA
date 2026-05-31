namespace Beacon.Application.Features.Authorization.Dtos;

public class CreateRoleRequest
{
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public IReadOnlyCollection<Guid>? PermissionIds { get; set; } = [];
}
