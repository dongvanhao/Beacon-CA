namespace Beacon.Application.Features.Identity.Dtos;

public class AdminLoginRequest
{
    public string Username { get; set; } = default!;
    public string Password { get; set; } = default!;
}
