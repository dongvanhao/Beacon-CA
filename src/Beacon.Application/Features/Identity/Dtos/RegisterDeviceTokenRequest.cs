using Beacon.Domain.Enums.Identity;

namespace Beacon.Application.Features.Identity.Dtos;

public record RegisterDeviceTokenRequest(
    string Token,
    DevicePlatform Platform,
    string? DeviceId,
    string? DeviceName,
    string? AppVersion);
