namespace Beacon.Application.Features.Authorization.Dtos;

public class UpdateRoleRequest
{
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}
