using Beacon.Domain.Enums.Identity;
using System.Text.Json.Serialization;

namespace Beacon.Application.Features.Identity.Dtos;

public record RegisterDeviceTokenRequest(
    string Token,
    [property: JsonConverter(typeof(JsonStringEnumConverter))]
    DevicePlatform Platform,
    string? DeviceId,
    string? DeviceName,
    string? AppVersion);
