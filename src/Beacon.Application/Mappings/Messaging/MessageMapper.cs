using Beacon.Application.Features.Messaging.Dtos;
using Beacon.Domain.Entities.Messaging;

namespace Beacon.Application.Mappings.Messaging;

public sealed class MessageMapper
{
    public MessageDto ToDto(Message m, string senderUsername)
        => new(m.Id, m.GroupId, m.SenderId, senderUsername, m.Content, m.CreatedAtUtc);
}
