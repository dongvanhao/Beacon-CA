using Beacon.Application.Features.Posts.Dtos;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Posts.Queries.GetMyPosts;

public record GetMyPostsQuery(Guid CurrentUserId, string? Cursor, int Limit) : IRequest<Result<FeedResponse>>;
