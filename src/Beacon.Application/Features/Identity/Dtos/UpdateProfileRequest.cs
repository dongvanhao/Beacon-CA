namespace Beacon.Application.Features.Identity.Dtos;

public class UpdateProfileRequest
{
    public string FamilyName { get; set; } = default!;
    public string GivenName { get; set; } = default!;
    public string? PhoneNumber { get; set; }
    public string Email { get; set; } = default!;
}

