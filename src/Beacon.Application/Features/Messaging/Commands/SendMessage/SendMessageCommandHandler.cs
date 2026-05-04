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
    MessageMapper mapper)
    : IRequestHandler<SendMessageCommand, Result<MessageDto>>
{
    public async Task<Result<MessageDto>> Handle(SendMessageCommand command, CancellationToken ct)
    {
        if (!await groupRepo.IsMemberAsync(command.GroupId, currentUser.UserId, ct))
            return Result<MessageDto>.Failure(
                Error.Forbidden(ErrorCodes.Messaging.MESSAGE_GROUP_FORBIDDEN, "Bạn không phải thành viên của nhóm này."));

        var message = new Message
        {
            GroupId = command.GroupId,
            SenderId = currentUser.UserId,
            Content = command.Content,
            CreatedAtUtc = DateTime.UtcNow
        };

        await messageRepo.AddAsync(message, ct);
        await messageRepo.SaveChangesAsync(ct);

        return Result<MessageDto>.Success(mapper.ToDto(message, currentUser.FamilyName, currentUser.GivenName));
    }
}
