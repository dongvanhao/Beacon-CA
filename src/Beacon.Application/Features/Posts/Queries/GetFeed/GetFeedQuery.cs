using Beacon.Application.Features.Posts.Dtos;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Posts.Queries.GetFeed;

public record GetFeedQuery(Guid CurrentUserId, string? Cursor, int Limit) : IRequest<Result<FeedResponse>>;
