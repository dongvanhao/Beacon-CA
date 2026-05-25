using Beacon.Application.Features.Messaging.Dtos;
using Beacon.Domain.Entities.Messaging;

namespace Beacon.Application.Mappings.Messaging;

public sealed class MessageGroupDetailMapper
{
    public MessageGroupDetailDto ToDetailDto(
        MessageGroup group, string? displayName, string? displayAvatarUrl,
        MessageGroupMemberSettingDto setting,
        IReadOnlyList<MessageGroupMemberDto> members)
        => new(group.Id, group.Type, group.CreatedAtUtc, group.RequireApprovalToAddMembers, displayName, displayAvatarUrl, setting, members);

    public MessageGroupMemberDto ToMemberDto(MessageGroupMember member, string? avatarUrl)
        => new(
            UserId: member.UserId,
            FamilyName: member.User.FamilyName,
            GivenName: member.User.GivenName,
            AvatarUrl: avatarUrl,
            Role: member.Role,
            Status: member.Status,
            LastSeenMessageId: member.LastSeenMessageId);
}
