using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Messaging.Commands.SendMessage;
using Beacon.Application.Features.Messaging.Helpers;
using Beacon.Domain.Entities.Messaging;
using Beacon.Domain.Enums.Group;
using Beacon.Domain.Enums.Messaging;
using Beacon.Domain.IRepository;
using Beacon.Domain.IRepository.Messaging;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Messaging.Commands.AddGroupMember;

public class AddGroupMemberCommandHandler(
    IMessageGroupRepository groupRepo,
    IUserRepository userRepo,
    ICurrentUserService currentUser,
    INotificationService notificationService,
    ISender sender)
    : IRequestHandler<AddGroupMemberCommand, Result>
{
    public async Task<Result> Handle(AddGroupMemberCommand command, CancellationToken ct)
    {
        var targetUserIds = command.TargetUserIds
            .Distinct()
            .ToList();

        var group = await groupRepo.GetByIdWithMembersAsync(command.GroupId, ct);
        if (group is null || group.IsDeleted)
            return Result.Failure(Error.NotFound(ErrorCodes.Messaging.MESSAGE_GROUP_NOT_FOUND, "Không tìm thấy nhóm chat."));

        if (group.Type == MessageGroupType.Direct)
            return Result.Failure(Error.Validation(ErrorCodes.Validation.VALIDATION_ERROR, "Không thể thêm thành viên vào chat 1-1."));

        var callerMember = group.Members.FirstOrDefault(m => m.UserId == currentUser.UserId
            && m.Status == MessageGroupMemberStatus.Joined);
        if (callerMember is null)
            return Result.Failure(Error.Forbidden(ErrorCodes.Messaging.MESSAGE_GROUP_FORBIDDEN, "Ban khong phai thanh vien cua nhom nay."));

        var targetUsers = new List<Beacon.Domain.Entities.Identity.User>(targetUserIds.Count);
        foreach (var targetUserId in targetUserIds)
        {
            var targetUser = await userRepo.GetByIdAsync(targetUserId, ct);
            if (targetUser is null)
                return Result.Failure(Error.NotFound(ErrorCodes.Identity.USER_NOT_FOUND, "Có người dùng không tồn tại."));

            targetUsers.Add(targetUser);
        }

        if (group.Members.Any(m => targetUserIds.Contains(m.UserId)))
            return Result.Failure(Error.Conflict(ErrorCodes.Messaging.GROUP_MEMBER_ALREADY_EXISTS, "Có người dùng đã là thành viên của nhóm."));

        var status = !group.RequireApprovalToAddMembers
            ? MessageGroupMemberStatus.Joined
            : callerMember.Role is GroupMemberRole.Owner or GroupMemberRole.Manager
                ? MessageGroupMemberStatus.Joined
                : MessageGroupMemberStatus.PendingApproval;

        foreach (var targetUserId in targetUserIds)
        {
            group.Members.Add(new MessageGroupMember
            {
                GroupId = group.Id,
                UserId = targetUserId,
                Role = GroupMemberRole.Member,
                Status = status,
                JoinedAtUtc = DateTime.UtcNow,
                InvitedByUserId = currentUser.UserId
            });
        }
        await groupRepo.SaveChangesAsync(ct);

        var targetNames = string.Join(", ", targetUsers
            .Select(u => FormatName(u.FamilyName, u.GivenName, "thành viên")));
        var actorName = FormatName(currentUser.FamilyName, currentUser.GivenName, "Một thành viên");
        var content = status == MessageGroupMemberStatus.Joined
            ? $"{actorName} đã thêm {targetNames} vào nhóm"
            : $"{actorName} đã đề xuất thêm {targetNames} vào nhóm";
        var sendResult = await sender.Send(new SendMessageCommand(
            command.GroupId,
            content.Trim(),
            null,
            null,
            MessageType.MemberAdded,
            MessageMetadataHelper.Serialize(new
            {
                actorUserId = currentUser.UserId,
                members = targetUsers.Select(u => new
                {
                    userId = u.Id,
                    familyName = u.FamilyName,
                    givenName = u.GivenName,
                    avatarUrl = (string?)null,
                    role = (int)GroupMemberRole.Member,
                    status = (int)status,
                    lastSeenMessageId = (Guid?)null
                })
            })), ct);
        if (sendResult.IsFailure)
            return Result.Failure(sendResult.Error);

        var inviterName = $"{currentUser.GivenName} {currentUser.FamilyName}".Trim();
        var groupName = group.Name ?? "nhóm chat";
        foreach (var targetUserId in targetUserIds)
        {
            await notificationService.CreateAndDeliverAsync(
                targetUserId,
                NotificationType.GroupInvite,
                "Được mời vào nhóm",
                $"{inviterName} đã mời bạn vào {groupName}",
                ct: ct);
        }

        return Result.Success();
    }

    private static string FormatName(string? familyName, string? givenName, string fallback)
    {
        var name = $"{familyName} {givenName}".Trim();
        return string.IsNullOrWhiteSpace(name) ? fallback : name;
    }
}
