namespace Beacon.Application.Features.AccountManagement.Dtos;

public class AdminAccountRoleDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}
