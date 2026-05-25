using Beacon.Shared.Results;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Beacon.Application.Features.Messaging.Commands.UpdateGroupAvatar;

public record UpdateGroupAvatarCommand(Guid GroupId, IFormFile File) : IRequest<Result>;
