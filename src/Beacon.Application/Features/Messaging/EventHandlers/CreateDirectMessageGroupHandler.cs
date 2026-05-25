using Beacon.Application.Features.Group.Events;
using Beacon.Domain.Entities.Messaging;
using Beacon.Domain.Enums.Messaging;
using Beacon.Domain.IRepository.Messaging;
using MediatR;

namespace Beacon.Application.Features.Messaging.EventHandlers;

public class CreateDirectMessageGroupHandler(
    IMessageGroupRepository groupRepo,
    IMessageGroupMemberSettingRepository settingRepo)
    : INotificationHandler<FriendRequestAcceptedEvent>
{
    public async Task Handle(FriendRequestAcceptedEvent ev, CancellationToken ct)
    {
        var directKey = MessageGroup.BuildDirectKey(ev.SenderId, ev.ReceiverId);

        var existing = await groupRepo.GetByDirectKeyIncludingDeletedAsync(directKey, ct);
        if (existing is not null)
        {
            if (!existing.IsDeleted) return;

            existing.Restore();
            AddMemberIfMissing(existing, ev.SenderId);
            AddMemberIfMissing(existing, ev.ReceiverId);
            await settingRepo.AddIfNotExistsAsync(existing.Id, ev.SenderId, ct);
            await settingRepo.AddIfNotExistsAsync(existing.Id, ev.ReceiverId, ct);
            await groupRepo.SaveChangesAsync(ct);
            return;
        }

        var group = new MessageGroup
        {
            Type = MessageGroupType.Direct,
            DirectKey = directKey,
            CreatedAtUtc = DateTime.UtcNow
        };

        group.Members.Add(new MessageGroupMember
        {
            GroupId = group.Id,
            UserId = ev.SenderId,
            Role = GroupMemberRole.Member,
            Status = MessageGroupMemberStatus.Joined,
            JoinedAtUtc = DateTime.UtcNow
        });
        group.Members.Add(new MessageGroupMember
        {
            GroupId = group.Id,
            UserId = ev.ReceiverId,
            Role = GroupMemberRole.Member,
            Status = MessageGroupMemberStatus.Joined,
            JoinedAtUtc = DateTime.UtcNow
        });

        await groupRepo.AddAsync(group, ct);
        await settingRepo.AddAsync(MessageGroupMemberSetting.Create(group.Id, ev.SenderId), ct);
        await settingRepo.AddAsync(MessageGroupMemberSetting.Create(group.Id, ev.ReceiverId), ct);
        await groupRepo.SaveChangesAsync(ct);
    }

    private static void AddMemberIfMissing(MessageGroup group, Guid userId)
    {
        if (!group.Members.Any(m => m.UserId == userId))
            group.Members.Add(new MessageGroupMember
            {
                GroupId = group.Id,
                UserId = userId,
                Role = GroupMemberRole.Member,
                Status = MessageGroupMemberStatus.Joined,
                JoinedAtUtc = DateTime.UtcNow
            });
    }
}
