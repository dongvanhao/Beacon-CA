namespace Beacon.Application.Features.Identity.Dtos;

public class AdminLogoutRequest
{
    public string RefreshToken { get; set; } = default!;
}
