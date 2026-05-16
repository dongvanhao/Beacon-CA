using Beacon.Application.Features.Posts.Dtos;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Posts.Commands.UpdatePost;

public record UpdatePostCommand(Guid PostId, UpdatePostRequest Request, Guid CurrentUserId)
    : IRequest<Result<PostResponse>>;
