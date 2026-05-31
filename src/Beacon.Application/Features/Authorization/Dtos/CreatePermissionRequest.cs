namespace Beacon.Application.Features.Authorization.Dtos;

public class CreatePermissionRequest
{
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public string? Group { get; set; }
}
