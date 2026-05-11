using Beacon.Application.Features.Messaging.Dtos;
using Beacon.Domain.IRepository.Messaging;

namespace Beacon.Application.Mappings.Messaging;

public sealed class MessageGroupMapper
{
    public MessageGroupDto ToDto(MessageGroupSummary s, string? resolvedAvatarUrl = null)
        => new(s.GroupId, s.Type, s.PeerUserId, s.CreatedAtUtc,
               s.LastMessageId,
               s.LastMessageContent, s.LastMessageAtUtc,
               s.LastMessageSenderFamilyName, s.LastMessageSenderGivenName,
               s.LastSeenMessageId, s.IsSeenLatest, s.UnreadCount,
               s.DisplayName, resolvedAvatarUrl);
}
