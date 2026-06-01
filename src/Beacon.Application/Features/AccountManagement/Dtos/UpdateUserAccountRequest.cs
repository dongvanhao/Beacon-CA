namespace Beacon.Application.Features.AccountManagement.Dtos;

public class UpdateUserAccountRequest
{
    public string Username { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string FamilyName { get; set; } = default!;
    public string GivenName { get; set; } = default!;
    public string? PhoneNumber { get; set; }
    public string TimeZone { get; set; } = default!;
    public string? Password { get; set; }
    public bool IsActive { get; set; }
    public bool IsEmailVerified { get; set; }
}
