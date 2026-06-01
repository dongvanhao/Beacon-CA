namespace Beacon.Application.Features.AccountManagement.Dtos;

public class CreateAdminAccountRequest
{
    public string Username { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string Password { get; set; } = default!;
    public IReadOnlyCollection<Guid>? RoleIds { get; set; } = [];
}
