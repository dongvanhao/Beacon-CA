using Beacon.Application.Features.Posts.Dtos;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Posts.Commands.CreatePost;

public record CreatePostCommand(CreatePostRequest Request, Guid CurrentUserId)
    : IRequest<Result<PostResponse>>;
