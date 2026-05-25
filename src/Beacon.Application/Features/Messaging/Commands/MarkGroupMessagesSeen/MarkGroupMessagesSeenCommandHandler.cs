using Beacon.Application.Common.Interfaces.IService;
using Beacon.Domain.IRepository.Messaging;
using Beacon.Domain.Enums.Messaging;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Messaging.Commands.MarkGroupMessagesSeen;

public class MarkGroupMessagesSeenCommandHandler(
    IMessageGroupRepository groupRepo,
    IMessageRepository messageRepo,
    IRealtimeNotifier notifier)
    : IRequestHandler<MarkGroupMessagesSeenCommand, Result>
{
    public async Task<Result> Handle(MarkGroupMessagesSeenCommand command, CancellationToken ct)
    {
        var group = await groupRepo.GetByIdAsync(command.GroupId, ct);
        if (group is null)
            return Result.Failure(
                Error.NotFound(ErrorCodes.Messaging.MESSAGE_GROUP_NOT_FOUND, "Nhóm chat không tồn tại."));

        var member = group.Members.FirstOrDefault(m => m.UserId == command.UserId
            && m.Status == MessageGroupMemberStatus.Joined);
        if (member is null)
            return Result.Failure(
                Error.Forbidden(ErrorCodes.Messaging.MESSAGE_GROUP_FORBIDDEN, "Bạn không phải thành viên của nhóm này."));

        if (!await messageRepo.ExistsInGroupAsync(command.GroupId, command.LastSeenMessageId, ct))
            return Result.Failure(
                Error.NotFound(ErrorCodes.Messaging.MESSAGE_NOT_FOUND, "Tin nhắn không tồn tại trong nhóm."));

        member.LastSeenMessageId = command.LastSeenMessageId;
        await groupRepo.SaveChangesAsync(ct);

        await notifier.NotifyMessageSeenAsync(command.GroupId, command.UserId, command.LastSeenMessageId, ct);
        await notifier.NotifyMessageGroupSeenAsync(command.UserId, command.GroupId, command.LastSeenMessageId, ct);
        var unreadCount = await messageRepo.CountUnreadAsync(command.GroupId, command.LastSeenMessageId, ct);
        await notifier.NotifyUnreadMessageCountAsync(command.UserId, command.GroupId, unreadCount, ct);

        return Result.Success();
    }
}
