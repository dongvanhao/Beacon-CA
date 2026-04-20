using Beacon.Application.Features.Identity.Dtos;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Identity.Commands.UpdateAvatar;

public record UpdateAvatarCommand(Guid UserId, Guid MediaObjectId) : IRequest<Result<UserProfileDto>>;
