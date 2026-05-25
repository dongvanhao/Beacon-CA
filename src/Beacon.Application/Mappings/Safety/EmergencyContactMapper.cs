using Beacon.Application.Features.Safety.Dtos;
using Beacon.Domain.Entities.Safety;

namespace Beacon.Application.Mappings.Safety;

public sealed class EmergencyContactMapper
{
    public EmergencyContactDto ToDto(EmergencyContact contact) => new()
    {
        Id = contact.Id,
        FullName = contact.FullName,
        ContactValue = contact.ContactValue,
        Relationship = contact.Relationship,
        ChannelType = contact.ChannelType.ToString(),
        PriorityOrder = contact.PriorityOrder,
        IsPrimary = contact.IsPrimary,
        IsActive = contact.IsActive
    };
}
