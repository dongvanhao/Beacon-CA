namespace Beacon.Application.Features.AccountManagement.Dtos;

public class AdminAccountDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public bool IsActive { get; set; }
    public DateTime? LastLoginAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public List<AdminAccountRoleDto> Roles { get; set; } = [];
}
