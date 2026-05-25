namespace Beacon.Application.Features.Safety.Dtos;

public record EmergencyContactDto
{
    public Guid Id { get; init; }
    public string FullName { get; init; } = default!;
    public string ContactValue { get; init; } = default!;
    public string? Relationship { get; init; }
    public string ChannelType { get; init; } = default!;
    public int PriorityOrder { get; init; }
    public bool IsPrimary { get; init; }
    public bool IsActive { get; init; }
}
