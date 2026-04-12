namespace Beacon.Application.Features.Identity.Dtos;

public class LogoutRequest
{
    public string RefreshToken { get; set; } = default!;
}
