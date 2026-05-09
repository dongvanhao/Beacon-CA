using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Identity.Commands.RevokeDeviceToken;

public record RevokeDeviceTokenCommand(Guid UserId, string Token) : IRequest<Result>;
