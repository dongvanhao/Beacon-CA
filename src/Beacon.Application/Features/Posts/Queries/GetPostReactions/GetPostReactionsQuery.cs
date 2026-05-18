using Beacon.Application.Features.Posts.Dtos;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Posts.Queries.GetPostReactions;

public record GetPostReactionsQuery(
    Guid PostId,
    Guid CurrentUserId,
    string? Icon,
    string? Cursor,
    int Limit
) : IRequest<Result<PostReactionListResponse>>;
