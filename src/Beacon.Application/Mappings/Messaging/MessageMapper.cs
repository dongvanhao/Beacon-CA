using Beacon.Application.Features.Messaging.Dtos;
using Beacon.Domain.Entities.Messaging;

namespace Beacon.Application.Mappings.Messaging;

public sealed class MessageMapper
{
    public MessageDto ToDto(Message m, string senderFamilyName, string senderGivenName)
        => new(m.Id, m.GroupId, m.SenderId, senderFamilyName, senderGivenName, m.Content, m.CreatedAtUtc);
}
