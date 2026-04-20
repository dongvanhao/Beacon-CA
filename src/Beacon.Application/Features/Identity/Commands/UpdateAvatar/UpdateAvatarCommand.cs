using Beacon.Application.Features.Identity.Dtos;
using Beacon.Shared.Results;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Beacon.Application.Features.Identity.Commands.UpdateAvatar;

public record UpdateAvatarCommand(IFormFile File, Guid UserId) : IRequest<Result<UserProfileDto>>;
