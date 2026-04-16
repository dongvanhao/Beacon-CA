namespace Beacon.Application.Features.Identity.Dtos;

public class UserProfileDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string? PhoneNumber { get; set; }
    public string TimeZone { get; set; } = default!;
    public bool IsActive { get; set; }
    public bool IsEmailVerified { get; set; }
    public DateTime? LastLoginAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
