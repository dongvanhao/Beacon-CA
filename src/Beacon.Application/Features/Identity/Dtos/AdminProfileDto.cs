namespace Beacon.Application.Features.Identity.Dtos;

public class AdminProfileDto
{
    public Guid AdminId { get; set; }
    public string Username { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public bool IsActive { get; set; }
    public DateTime? LastLoginAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public IEnumerable<string> Roles { get; set; } = [];
    public IEnumerable<string> Permissions { get; set; } = [];
}
