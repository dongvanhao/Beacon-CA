using Beacon.Application.Common.Interfaces.IService;
using Beacon.Domain.Entities.Messaging;
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
    ICurrentUserService currentUser)
    : IRequestHandler<AddGroupMemberCommand, Result>
{
    public async Task<Result> Handle(AddGroupMemberCommand command, CancellationToken ct)
    {
        var group = await groupRepo.GetByIdWithMembersAsync(command.GroupId, ct);
        if (group is null || group.IsDeleted)
            return Result.Failure(Error.NotFound(ErrorCodes.Messaging.MESSAGE_GROUP_NOT_FOUND, "Không tìm thấy nhóm chat."));

        if (group.IsPrivate)
            return Result.Failure(Error.Validation(ErrorCodes.Validation.VALIDATION_ERROR, "Không thể thêm thành viên vào chat 1-1."));

        var callerMember = group.Members.FirstOrDefault(m => m.UserId == currentUser.UserId);
        if (callerMember?.Role != GroupMemberRole.Owner)
            return Result.Failure(Error.Forbidden(ErrorCodes.Messaging.MESSAGE_GROUP_FORBIDDEN, "Chỉ owner mới được thêm thành viên."));

        if (!await userRepo.ExistsAsync(command.TargetUserId, ct))
            return Result.Failure(Error.NotFound(ErrorCodes.Identity.USER_NOT_FOUND, "Người dùng không tồn tại."));

        if (group.Members.Any(m => m.UserId == command.TargetUserId))
            return Result.Failure(Error.Conflict(ErrorCodes.Messaging.GROUP_MEMBER_ALREADY_EXISTS, "Người dùng đã là thành viên của nhóm."));

        group.Members.Add(new MessageGroupMember
        {
            GroupId = group.Id,
            UserId = command.TargetUserId,
            Role = GroupMemberRole.Member,
            JoinedAtUtc = DateTime.UtcNow,
            InvitedByUserId = currentUser.UserId
        });
        await groupRepo.SaveChangesAsync(ct);

        return Result.Success();
    }
}
