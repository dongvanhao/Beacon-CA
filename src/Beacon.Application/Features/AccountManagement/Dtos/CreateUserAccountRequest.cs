namespace Beacon.Application.Features.AccountManagement.Dtos;

public class CreateUserAccountRequest
{
    public string Username { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string Password { get; set; } = default!;
    public string FamilyName { get; set; } = default!;
    public string GivenName { get; set; } = default!;
    public string? PhoneNumber { get; set; }
    public string TimeZone { get; set; } = "Asia/Ha_Noi";
    public bool IsActive { get; set; } = true;
    public bool IsEmailVerified { get; set; }
}
