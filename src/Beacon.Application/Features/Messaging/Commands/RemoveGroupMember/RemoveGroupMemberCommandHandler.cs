using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Messaging.Commands.SendMessage;
using Beacon.Application.Features.Messaging.Helpers;
using Beacon.Domain.Enums.Group;
using Beacon.Domain.Enums.Messaging;
using Beacon.Domain.IRepository.Messaging;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Messaging.Commands.RemoveGroupMember;

public class RemoveGroupMemberCommandHandler(
    IMessageGroupRepository groupRepo,
    ICurrentUserService currentUser,
    INotificationService notificationService,
    ISender sender)
    : IRequestHandler<RemoveGroupMemberCommand, Result>
{
    public async Task<Result> Handle(RemoveGroupMemberCommand command, CancellationToken ct)
    {
        var group = await groupRepo.GetByIdWithMembersAsync(command.GroupId, ct);
        if (group is null || group.IsDeleted)
            return Result.Failure(Error.NotFound(ErrorCodes.Messaging.MESSAGE_GROUP_NOT_FOUND, "Không tìm thấy nhóm chat."));

        var callerMember = group.Members.FirstOrDefault(m => m.UserId == currentUser.UserId
            && m.Status == MessageGroupMemberStatus.Joined);
        if (callerMember is null || callerMember.Role is not (GroupMemberRole.Owner or GroupMemberRole.Manager))
            return Result.Failure(Error.Forbidden(ErrorCodes.Messaging.MESSAGE_GROUP_FORBIDDEN, "Chỉ owner hoặc manager mới được xóa thành viên."));

        if (command.TargetUserId == currentUser.UserId)
            return Result.Failure(Error.Validation(ErrorCodes.Validation.VALIDATION_ERROR, "Không thể tự remove — dùng LeaveGroup."));

        var targetMember = group.Members.FirstOrDefault(m => m.UserId == command.TargetUserId
            && m.Status == MessageGroupMemberStatus.Joined);
        if (targetMember is null)
            return Result.Failure(Error.NotFound(ErrorCodes.Messaging.GROUP_MEMBER_NOT_FOUND, "Người dùng không phải thành viên của nhóm."));

        if (targetMember.Role == GroupMemberRole.Owner)
            return Result.Failure(Error.Forbidden(ErrorCodes.Messaging.MESSAGE_GROUP_FORBIDDEN, "Không thể xóa owner."));

        if (callerMember.Role == GroupMemberRole.Manager && targetMember.Role != GroupMemberRole.Member)
            return Result.Failure(Error.Forbidden(ErrorCodes.Messaging.MESSAGE_GROUP_FORBIDDEN, "Manager chỉ được xóa member."));

        var actorName = FormatName(currentUser.FamilyName, currentUser.GivenName, "Một thành viên");
        var targetName = FormatName(targetMember.User.FamilyName, targetMember.User.GivenName, "thành viên");
        var sendResult = await sender.Send(new SendMessageCommand(
            command.GroupId,
            $"{actorName} đã xóa {targetName} khỏi nhóm".Trim(),
            null,
            null,
            MessageType.MemberLeft,
            MessageMetadataHelper.Serialize(new
            {
                actorUserId = currentUser.UserId,
                userId = command.TargetUserId,
                removedByUserId = currentUser.UserId
            })), ct);
        if (sendResult.IsFailure)
            return Result.Failure(sendResult.Error);

        await groupRepo.RemoveMemberAsync(command.GroupId, command.TargetUserId, ct);
        await groupRepo.SaveChangesAsync(ct);

        var groupName = group.Name ?? "nhóm chat";
        await notificationService.CreateAndDeliverAsync(
            command.TargetUserId,
            NotificationType.GroupRemoved,
            "Đã bị xóa khỏi nhóm",
            $"Bạn đã bị xóa khỏi {groupName}",
            ct: ct);

        return Result.Success();
    }

    private static string FormatName(string? familyName, string? givenName, string fallback)
    {
        var name = $"{familyName} {givenName}".Trim();
        return string.IsNullOrWhiteSpace(name) ? fallback : name;
    }
}
