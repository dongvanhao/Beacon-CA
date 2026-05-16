using Beacon.Application.Features.Posts.Dtos;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Posts.Commands.UpsertReaction;

public record UpsertPostReactionCommand(
    Guid PostId,
    string Icon,
    Guid CurrentUserId
) : IRequest<Result<PostReactionResponse>>;
