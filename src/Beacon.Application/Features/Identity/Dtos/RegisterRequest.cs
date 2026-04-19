namespace Beacon.Application.Features.Identity.Dtos;

public class RegisterRequest
{
    public string Username { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string Password { get; set; } = default!;
    public string FamilyName { get; set; } = default!;
    public string GivenName { get; set; } = default!;
    public string? PhoneNumber { get; set; }
}
