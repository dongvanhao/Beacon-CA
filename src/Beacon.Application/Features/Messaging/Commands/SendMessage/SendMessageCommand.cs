using Beacon.Application.Features.Messaging.Dtos;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Messaging.Commands.SendMessage;

public record SendMessageCommand(Guid GroupId, string Content, string? ClientMessageId) : IRequest<Result<MessageDto>>;
