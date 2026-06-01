namespace Beacon.Application.Features.AccountManagement.Dtos;

public class UpdateAdminAccountRequest
{
    public string Username { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string? Password { get; set; }
    public bool IsActive { get; set; }
    public IReadOnlyCollection<Guid>? RoleIds { get; set; }
}
