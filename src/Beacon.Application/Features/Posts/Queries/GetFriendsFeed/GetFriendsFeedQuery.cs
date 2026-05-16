using Beacon.Application.Features.Posts.Dtos;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Posts.Queries.GetFriendsFeed;

public record GetFriendsFeedQuery(Guid CurrentUserId, Guid FriendId, string? Cursor, int Limit) : IRequest<Result<FeedResponse>>;
