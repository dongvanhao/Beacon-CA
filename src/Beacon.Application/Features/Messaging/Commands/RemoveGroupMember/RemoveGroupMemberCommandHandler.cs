using Beacon.Application.Common.Interfaces.IService;
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
    INotificationService notificationService)
    : IRequestHandler<RemoveGroupMemberCommand, Result>
{
    public async Task<Result> Handle(RemoveGroupMemberCommand command, CancellationToken ct)
    {
        var group = await groupRepo.GetByIdWithMembersAsync(command.GroupId, ct);
        if (group is null || group.IsDeleted)
            return Result.Failure(Error.NotFound(ErrorCodes.Messaging.MESSAGE_GROUP_NOT_FOUND, "Không tìm thấy nhóm chat."));

        var callerMember = group.Members.FirstOrDefault(m => m.UserId == currentUser.UserId);
        if (callerMember?.Role != GroupMemberRole.Owner)
            return Result.Failure(Error.Forbidden(ErrorCodes.Messaging.MESSAGE_GROUP_FORBIDDEN, "Chỉ owner mới được xóa thành viên."));

        if (command.TargetUserId == currentUser.UserId)
            return Result.Failure(Error.Validation(ErrorCodes.Validation.VALIDATION_ERROR, "Owner không thể tự remove — dùng LeaveGroup sau khi transfer ownership."));

        if (!group.Members.Any(m => m.UserId == command.TargetUserId))
            return Result.Failure(Error.NotFound(ErrorCodes.Messaging.GROUP_MEMBER_NOT_FOUND, "Người dùng không phải thành viên của nhóm."));

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
}
