using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Messaging.Dtos;
using Beacon.Application.Mappings.Messaging;
using Beacon.Domain.Entities.Messaging;
using Beacon.Domain.IRepository.Messaging;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Messaging.Commands.SendMessage;

public class SendMessageCommandHandler(
    IMessageGroupRepository groupRepo,
    IMessageRepository messageRepo,
    ICurrentUserService currentUser,
    IRealtimeNotifier notifier,
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

        await messageRepo.AddAsync(message, ct);
        await messageRepo.SaveChangesAsync(ct);

        var dto = mapper.ToDto(message, currentUser.FamilyName, currentUser.GivenName);

        var memberIds = group.Members
            .Where(m => m.UserId != currentUser.UserId)
            .Select(m => m.UserId);
        await notifier.NotifyNewMessageAsync(command.GroupId, memberIds, dto, ct);

        return Result<MessageDto>.Success(dto);
    }
}
