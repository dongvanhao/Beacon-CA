namespace Beacon.Application.Features.Authorization.Dtos;

public class AdminRoleAssignmentDto
{
    public Guid AdminId { get; set; }
    public string Username { get; set; } = default!;
    public Guid RoleId { get; set; }
    public string RoleName { get; set; } = default!;
    public DateTime AssignedAtUtc { get; set; }
}
