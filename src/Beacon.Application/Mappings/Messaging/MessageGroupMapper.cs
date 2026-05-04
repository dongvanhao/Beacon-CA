using Beacon.Application.Features.Messaging.Dtos;
using Beacon.Domain.IRepository.Messaging;

namespace Beacon.Application.Mappings.Messaging;

public sealed class MessageGroupMapper
{
    public MessageGroupDto ToDto(MessageGroupSummary s)
        => new(s.GroupId, s.IsPrivate, s.CreatedAtUtc,
               s.LastMessageContent, s.LastMessageAtUtc,
               s.LastMessageSenderFamilyName, s.LastMessageSenderGivenName,
               s.PeerFamilyName, s.PeerGivenName);
}
