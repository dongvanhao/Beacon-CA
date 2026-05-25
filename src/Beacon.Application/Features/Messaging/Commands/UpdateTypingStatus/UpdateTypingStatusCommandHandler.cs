using Beacon.Application.Common.Interfaces.IService;
using Beacon.Domain.IRepository.Messaging;
using Beacon.Domain.Enums.Messaging;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Messaging.Commands.UpdateTypingStatus;

public class UpdateTypingStatusCommandHandler(
    IMessageGroupRepository groupRepo,
    IRealtimeNotifier notifier)
    : IRequestHandler<UpdateTypingStatusCommand, Result>
{
    public async Task<Result> Handle(UpdateTypingStatusCommand command, CancellationToken ct)
    {
        var group = await groupRepo.GetByIdAsync(command.GroupId, ct);
        if (group is null)
            return Result.Failure(
                Error.NotFound(ErrorCodes.Messaging.MESSAGE_GROUP_NOT_FOUND, "Nhóm chat không tồn tại."));

        if (!group.Members.Any(m => m.UserId == command.UserId
                && m.Status == MessageGroupMemberStatus.Joined))
            return Result.Failure(
                Error.Forbidden(ErrorCodes.Messaging.MESSAGE_GROUP_FORBIDDEN, "Bạn không phải thành viên của nhóm này."));

        await notifier.NotifyTypingAsync(command.GroupId, command.UserId, command.IsTyping, ct);

        return Result.Success();
    }
}
