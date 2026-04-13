namespace Beacon.Application.Features.Identity.Dtos;

public class RegisterRequest
{
    public string Username { get; set; } = default!;
    public string Password { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string? PhoneNumber { get; set; }
}
