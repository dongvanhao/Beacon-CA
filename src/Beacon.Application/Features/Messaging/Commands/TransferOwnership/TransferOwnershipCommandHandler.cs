using Beacon.Application.Common.Interfaces.IService;
using Beacon.Domain.Enums.Messaging;
using Beacon.Domain.IRepository.Messaging;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Messaging.Commands.TransferOwnership;

public class TransferOwnershipCommandHandler(
    IMessageGroupRepository groupRepo,
    ICurrentUserService currentUser)
    : IRequestHandler<TransferOwnershipCommand, Result>
{
    public async Task<Result> Handle(TransferOwnershipCommand command, CancellationToken ct)
    {
        var group = await groupRepo.GetByIdWithMembersAsync(command.GroupId, ct);
        if (group is null || group.IsDeleted)
            return Result.Failure(Error.NotFound(ErrorCodes.Messaging.MESSAGE_GROUP_NOT_FOUND, "Không tìm thấy nhóm chat."));

        var callerMember = group.Members.FirstOrDefault(m => m.UserId == currentUser.UserId);
        if (callerMember?.Role != GroupMemberRole.Owner)
            return Result.Failure(Error.Forbidden(ErrorCodes.Messaging.MESSAGE_GROUP_FORBIDDEN, "Chỉ owner mới được transfer ownership."));

        var newOwnerMember = group.Members.FirstOrDefault(m => m.UserId == command.NewOwnerUserId);
        if (newOwnerMember is null)
            return Result.Failure(Error.NotFound(ErrorCodes.Messaging.GROUP_MEMBER_NOT_FOUND, "Người dùng không phải thành viên nhóm."));

        callerMember.Role = GroupMemberRole.Member;
        newOwnerMember.Role = GroupMemberRole.Owner;
        await groupRepo.SaveChangesAsync(ct);

        return Result.Success();
    }
}
