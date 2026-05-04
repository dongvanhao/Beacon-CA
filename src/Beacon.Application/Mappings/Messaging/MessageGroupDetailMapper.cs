using Beacon.Application.Features.Messaging.Dtos;
using Beacon.Domain.Entities.Messaging;

namespace Beacon.Application.Mappings.Messaging;

public sealed class MessageGroupDetailMapper
{
    public MessageGroupDetailDto ToDetailDto(
        MessageGroup group, IReadOnlyList<MessageGroupMemberDto> members)
        => new(group.Id, group.IsPrivate, group.CreatedAtUtc, members);

    public MessageGroupMemberDto ToMemberDto(MessageGroupMember member, string? avatarUrl)
        => new(
            UserId: member.UserId,
            FamilyName: member.User.FamilyName,
            GivenName: member.User.GivenName,
            AvatarUrl: avatarUrl);
}
