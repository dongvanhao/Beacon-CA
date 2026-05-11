using System.Text.Json;
using Beacon.Application.Features.Identity.Dtos;
using Beacon.Domain.Enums.Identity;
using FluentAssertions;

namespace Beacon.UnitTests.Identity;

public class RegisterDeviceTokenRequestTests
{
    [Fact]
    public void Deserialize_ShouldAcceptLowercasePlatformString()
    {
        const string json = """
        {
          "token": "fcm-token",
          "platform": "android"
        }
        """;

        var dto = JsonSerializer.Deserialize<RegisterDeviceTokenRequest>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        dto.Should().NotBeNull();
        dto!.Platform.Should().Be(DevicePlatform.Android);
    }
}
