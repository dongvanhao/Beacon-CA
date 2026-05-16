using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Posts.Commands.DeletePost;

public record DeletePostCommand(Guid PostId, Guid CurrentUserId) : IRequest<Result<object?>>;
