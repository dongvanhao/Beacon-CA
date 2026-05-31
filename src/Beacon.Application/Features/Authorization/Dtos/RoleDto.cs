namespace Beacon.Application.Features.Authorization.Dtos;

public class RoleDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public IEnumerable<PermissionDto> Permissions { get; set; } = [];
}
