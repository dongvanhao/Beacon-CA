using Beacon.Application.Features.Messaging.Dtos;
using Beacon.Domain.Enums.Messaging;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Messaging.Commands.SendMessage;

public record SendMessageCommand(
    Guid? GroupId,
    string? Content,
    string? ClientMessageId,
    Guid? PostId = null,
    MessageType Type = MessageType.Normal,
    string? MetadataJson = null)
    : IRequest<Result<MessageDto>>;
