using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Messaging.Dtos;
using Beacon.Application.Mappings.Messaging;
using Beacon.Domain.Entities.Messaging;
using Beacon.Domain.IRepository.Messaging;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Beacon.Application.Features.Messaging.Commands.SendMessage;

public class SendMessageCommandHandler(
    IMessageGroupRepository groupRepo,
    IMessageRepository messageRepo,
    ICurrentUserService currentUser,
    IRealtimeNotifier notifier,
    IFcmService fcmService,
    IMessageGroupPresenceTracker presenceTracker,
    ILogger<SendMessageCommandHandler> logger,
    MessageMapper mapper)
    : IRequestHandler<SendMessageCommand, Result<MessageDto>>
{
    public async Task<Result<MessageDto>> Handle(SendMessageCommand command, CancellationToken ct)
    {
        // FIX-10: verify group exists (query filter excludes soft-deleted groups)
        var group = await groupRepo.GetByIdAsync(command.GroupId, ct);
        if (group is null)
            return Result<MessageDto>.Failure(
                Error.NotFound(ErrorCodes.Messaging.MESSAGE_GROUP_NOT_FOUND, "Nhóm chat không tồn tại hoặc đã bị xóa."));

        if (!group.Members.Any(m => m.UserId == currentUser.UserId))
            return Result<MessageDto>.Failure(
                Error.Forbidden(ErrorCodes.Messaging.MESSAGE_GROUP_FORBIDDEN, "Bạn không phải thành viên của nhóm này."));

        // FIX-02: idempotency — return existing message on retry (no notification re-push)
        if (command.ClientMessageId is not null)
        {
            var existing = await messageRepo.GetByClientMessageIdAsync(command.GroupId, command.ClientMessageId, ct);
            if (existing is not null)
                return Result<MessageDto>.Success(mapper.ToDto(existing, currentUser.FamilyName, currentUser.GivenName));
        }

        var message = Message.Create(command.GroupId, currentUser.UserId, command.Content, command.ClientMessageId);
        var senderMember = group.Members.First(m => m.UserId == currentUser.UserId);
        senderMember.LastSeenMessageId = message.Id;

        await messageRepo.AddAsync(message, ct);
        await messageRepo.SaveChangesAsync(ct);

        var dto = mapper.ToDto(message, currentUser.FamilyName, currentUser.GivenName);

        var recipientUserIds = group.Members
            .Select(m => m.UserId)
            .Distinct()
            .ToArray();

        await notifier.NotifyNewMessageAsync(command.GroupId, dto, recipientUserIds, ct);

        var fcmRecipientUserIds = recipientUserIds
            .Where(id => id != currentUser.UserId)
            .Where(id => !presenceTracker.IsUserInGroup(id, command.GroupId))
            .Distinct()
            .ToArray();

        foreach (var member in group.Members)
        {
            var unreadCount = await messageRepo.CountUnreadAsync(command.GroupId, member.LastSeenMessageId, ct);
            await notifier.NotifyUnreadMessageCountAsync(member.UserId, command.GroupId, unreadCount, ct);
        }

        if (fcmRecipientUserIds.Length > 0)
        {
            var senderName = $"{currentUser.GivenName} {currentUser.FamilyName}".Trim();
            var title = string.IsNullOrWhiteSpace(senderName) ? "Tin nhắn mới" : senderName;
            var body = string.IsNullOrWhiteSpace(command.Content) ? "Bạn có tin nhắn mới" : command.Content;

            var fcmData = new Dictionary<string, string>
            {
                ["type"] = "MESSAGE_NEW",
                ["messageGroupId"] = command.GroupId.ToString(),
                ["messageId"] = message.Id.ToString(),
                ["senderUserId"] = currentUser.UserId.ToString()
            };

            foreach (var userId in fcmRecipientUserIds)
            {
                try
                {
                    await fcmService.SendToUserAsync(userId, title, body, fcmData, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "FCM delivery failed for user {UserId}", userId);
                }
            }
        }

        return Result<MessageDto>.Success(dto);
    }
}
