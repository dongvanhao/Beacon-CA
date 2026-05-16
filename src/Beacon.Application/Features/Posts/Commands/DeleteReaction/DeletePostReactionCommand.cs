using Beacon.Application.Features.Posts.Dtos;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Posts.Commands.DeleteReaction;

public record DeletePostReactionCommand(
    Guid PostId,
    Guid CurrentUserId
) : IRequest<Result<PostReactionResponse>>;
