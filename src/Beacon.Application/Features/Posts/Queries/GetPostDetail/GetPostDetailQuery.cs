using Beacon.Application.Features.Posts.Dtos;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Posts.Queries.GetPostDetail;

public record GetPostDetailQuery(Guid PostId, Guid CurrentUserId) : IRequest<Result<PostDetailResponse>>;
