using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Messaging.Commands.SendMessage;
using Beacon.Application.Features.Messaging.Helpers;
using Beacon.Domain.Enums.Messaging;
using Beacon.Domain.IRepository.Messaging;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Messaging.Commands.ApproveGroupMember;

public class ApproveGroupMemberCommandHandler(
    IMessageGroupRepository groupRepo,
    ICurrentUserService currentUser,
    ISender sender)
    : IRequestHandler<ApproveGroupMemberCommand, Result>
{
    public async Task<Result> Handle(ApproveGroupMemberCommand command, CancellationToken ct)
    {
        var group = await groupRepo.GetByIdWithMembersAsync(command.GroupId, ct);
        if (group is null || group.IsDeleted)
            return Result.Failure(Error.NotFound(
                ErrorCodes.Messaging.MESSAGE_GROUP_NOT_FOUND,
                "Không tìm thấy nhóm chat."));

        if (group.Type == MessageGroupType.Direct)
            return Result.Failure(Error.Validation(
                ErrorCodes.Validation.VALIDATION_ERROR,
                "Không thể duyệt thành viên cho chat 1-1."));

        var callerMember = group.Members.FirstOrDefault(m => m.UserId == currentUser.UserId
            && m.Status == MessageGroupMemberStatus.Joined);
        if (callerMember is null || callerMember.Role is not (GroupMemberRole.Owner or GroupMemberRole.Manager))
            return Result.Failure(Error.Forbidden(
                ErrorCodes.Messaging.MESSAGE_GROUP_FORBIDDEN,
                "Chỉ owner hoặc manager mới được duyệt thành viên."));

        var targetMember = group.Members.FirstOrDefault(m => m.UserId == command.TargetUserId);
        if (targetMember is null)
            return Result.Failure(Error.NotFound(
                ErrorCodes.Messaging.GROUP_MEMBER_NOT_FOUND,
                "Người dùng không phải thành viên của nhóm."));

        if (targetMember.Status != MessageGroupMemberStatus.PendingApproval)
            return Result.Failure(Error.Conflict(
                ErrorCodes.Messaging.GROUP_MEMBER_NOT_PENDING,
                "Thành viên không ở trạng thái chờ duyệt."));

        targetMember.Status = MessageGroupMemberStatus.Joined;
        targetMember.JoinedAtUtc = DateTime.UtcNow;
        await groupRepo.SaveChangesAsync(ct);

        var actorName = FormatName(currentUser.FamilyName, currentUser.GivenName, "Một thành viên");
        var targetName = FormatName(targetMember.User.FamilyName, targetMember.User.GivenName, "thành viên");
        var sendResult = await sender.Send(new SendMessageCommand(
            command.GroupId,
            $"{actorName} đã duyệt {targetName} vào nhóm".Trim(),
            null,
            null,
            MessageType.MemberApproved,
            MessageMetadataHelper.Serialize(new
            {
                actorUserId = currentUser.UserId,
                member = MessageMetadataHelper.Member(targetMember)
            })), ct);
        if (sendResult.IsFailure)
            return Result.Failure(sendResult.Error);

        return Result.Success();
    }

    private static string FormatName(string? familyName, string? givenName, string fallback)
    {
        var name = $"{familyName} {givenName}".Trim();
        return string.IsNullOrWhiteSpace(name) ? fallback : name;
    }
}
