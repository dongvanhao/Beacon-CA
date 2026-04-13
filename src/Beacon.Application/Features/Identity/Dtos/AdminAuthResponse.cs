namespace Beacon.Application.Features.Identity.Dtos;

public class AdminAuthResponse
{
    public Guid AdminId { get; set; }
    public string Username { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string AccessToken { get; set; } = default!;
    public string RefreshToken { get; set; } = default!;
    public DateTime AccessTokenExpiresAt { get; set; }
    public IEnumerable<string> Permissions { get; set; } = [];
}
