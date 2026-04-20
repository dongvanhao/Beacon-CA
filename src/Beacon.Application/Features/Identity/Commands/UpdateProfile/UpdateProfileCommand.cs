using Beacon.Application.Features.Identity.Dtos;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Identity.Commands.UpdateProfile;

public record UpdateProfileCommand(Guid UserId, UpdateProfileRequest Request) : IRequest<Result<UserProfileDto>>;
