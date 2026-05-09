using Beacon.Domain.Enums.Identity;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Identity.Commands.RegisterDeviceToken;

public record RegisterDeviceTokenCommand(
    Guid UserId,
    string Token,
    DevicePlatform Platform,
    string? DeviceId,
    string? DeviceName,
    string? AppVersion) : IRequest<Result>;
