namespace Beacon.Application.Features.Identity.Dtos;

public class UpdateProfileRequest
{
    public string? FamilyName { get; set; }
    public string? GivenName { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
}


