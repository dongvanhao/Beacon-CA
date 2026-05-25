using Beacon.Domain.Enums;

namespace Beacon.Application.Features.Safety.Dtos;

public record CreateEmergencyContactRequest(
    string FullName,
    string ContactValue,
    ContactChannelType ChannelType,
    string? Relationship,
    int PriorityOrder);
