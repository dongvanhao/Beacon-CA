using Beacon.Application.Features.Group.Events;
using Beacon.Domain.Entities.Messaging;
using Beacon.Domain.Enums.Messaging;
using Beacon.Domain.IRepository.Messaging;
using MediatR;

namespace Beacon.Application.Features.Messaging.EventHandlers;

/// <summary>Reacts to FriendRequestAcceptedEvent: creates the private DM group + per-user settings.</summary>
public class CreateDirectMessageGroupHandler(
    IMessageGroupRepository groupRepo,
    IMessageGroupMemberSettingRepository settingRepo)
    : INotificationHandler<FriendRequestAcceptedEvent>
{
    public async Task Handle(FriendRequestAcceptedEvent ev, CancellationToken ct)
    {
        var group = new MessageGroup { IsPrivate = true, CreatedAtUtc = DateTime.UtcNow };

        group.Members.Add(new MessageGroupMember
        {
            GroupId = group.Id,
            UserId = ev.SenderId,
            Role = GroupMemberRole.Member,
            JoinedAtUtc = DateTime.UtcNow
        });
        group.Members.Add(new MessageGroupMember
        {
            GroupId = group.Id,
            UserId = ev.ReceiverId,
            Role = GroupMemberRole.Member,
            JoinedAtUtc = DateTime.UtcNow
        });

        await groupRepo.AddAsync(group, ct);

        await settingRepo.AddAsync(MessageGroupMemberSetting.Create(group.Id, ev.SenderId), ct);
        await settingRepo.AddAsync(MessageGroupMemberSetting.Create(group.Id, ev.ReceiverId), ct);

        await groupRepo.SaveChangesAsync(ct);
    }
}
