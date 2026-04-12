namespace Beacon.Application.Features.Identity.Dtos;

public class AdminLoginRequest
{
    public string Email { get; set; } = default!;
    public string Password { get; set; } = default!;
}
