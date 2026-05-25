using Beacon.Application.Common.Interfaces.IService;
using Beacon.Domain.Enums.Messaging;
using Beacon.Domain.IRepository.Messaging;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Messaging.Commands.DeleteGroup;

public class DeleteGroupCommandHandler(
    IMessageGroupRepository groupRepo,
    ICurrentUserService currentUser)
    : IRequestHandler<DeleteGroupCommand, Result>
{
    public async Task<Result> Handle(DeleteGroupCommand command, CancellationToken ct)
    {
        var group = await groupRepo.GetByIdWithMembersAsync(command.GroupId, ct);
        if (group is null || group.IsDeleted)
            return Result.Failure(Error.NotFound(
                ErrorCodes.Messaging.MESSAGE_GROUP_NOT_FOUND,
                "Khong tim thay nhom chat."));

        if (group.Type == MessageGroupType.Direct)
            return Result.Failure(Error.Validation(
                ErrorCodes.Validation.VALIDATION_ERROR,
                "Khong the xoa chat 1-1."));

        var callerMember = group.Members.FirstOrDefault(m => m.UserId == currentUser.UserId
            && m.Status == MessageGroupMemberStatus.Joined);
        if (callerMember?.Role != GroupMemberRole.Owner)
            return Result.Failure(Error.Forbidden(
                ErrorCodes.Messaging.MESSAGE_GROUP_FORBIDDEN,
                "Chi owner moi duoc xoa nhom."));

        group.Delete();
        await groupRepo.SaveChangesAsync(ct);

        return Result.Success();
    }
}
