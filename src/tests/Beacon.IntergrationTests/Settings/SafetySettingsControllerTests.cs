using Beacon.Application.Features.Settings.Dtos;
using Beacon.IntergrationTests.Common;
using Beacon.Shared.Common.Responses;
using Beacon.Shared.Constants;
using FluentAssertions;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Beacon.IntergrationTests.Settings;

public class SafetySettingsControllerTests : IClassFixture<BeaconWebApplicationFactory>
{
    private readonly HttpClient _client;
    private const string Endpoint = "/api/v1/safety/settings";

    public SafetySettingsControllerTests(BeaconWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ===================== GET =====================

    [Fact]
    public async Task Get_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync(Endpoint);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_WhenNoSettingExists_Returns200WithDefaultValues()
    {
        // Arrange — user chưa có setting
        var userId = Guid.NewGuid();
        AuthorizeAs(userId);

        // Act
        var response = await _client.GetAsync(Endpoint);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<SafetySettingDto>>();
        body!.Success.Should().BeTrue();
        body.Data.Should().NotBeNull();
        body.Data!.IsDefault.Should().BeTrue();
        body.Data.DailyDeadlineLocalTime.Should().Be("20:00");
        body.Data.GracePeriodMinutes.Should().Be(15);
        body.Data.ReminderBeforeMinutes.Should().Be(30);
        body.Data.AutoAlertDelayMinutes.Should().Be(15);
        body.Data.IsMonitoringEnabled.Should().BeTrue();
        body.Data.IsAutoAlertEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task Get_WhenSettingExists_Returns200WithActualData()
    {
        // Arrange — PATCH trước để tạo setting, rồi GET lại
        var userId = Guid.NewGuid();
        AuthorizeAs(userId);

        await _client.PatchAsJsonAsync(Endpoint, new
        {
            dailyDeadlineLocalTime = "22:30",
            gracePeriodMinutes     = 10,
            reminderBeforeMinutes  = 20,
            autoAlertDelayMinutes  = 5,
            isMonitoringEnabled    = false,
            isAutoAlertEnabled     = true
        });

        // Act
        var response = await _client.GetAsync(Endpoint);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<SafetySettingDto>>();
        body!.Success.Should().BeTrue();
        body.Data!.IsDefault.Should().BeFalse();
        body.Data.DailyDeadlineLocalTime.Should().Be("22:30");
        body.Data.GracePeriodMinutes.Should().Be(10);
        body.Data.ReminderBeforeMinutes.Should().Be(20);
        body.Data.IsMonitoringEnabled.Should().BeFalse();
    }

    // ===================== PATCH =====================

    [Fact]
    public async Task Patch_WithoutToken_Returns401()
    {
        var response = await _client.PatchAsJsonAsync(Endpoint, ValidRequest());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Patch_WhenNoExistingRecord_Creates200WithActualData()
    {
        // Arrange
        var userId = Guid.NewGuid();
        AuthorizeAs(userId);

        // Act
        var response = await _client.PatchAsJsonAsync(Endpoint, new
        {
            dailyDeadlineLocalTime = "21:00",
            gracePeriodMinutes     = 20,
            reminderBeforeMinutes  = 45,
            autoAlertDelayMinutes  = 10,
            isMonitoringEnabled    = true,
            isAutoAlertEnabled     = false
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<SafetySettingDto>>();
        body!.Success.Should().BeTrue();
        body.Data!.IsDefault.Should().BeFalse();
        body.Data.DailyDeadlineLocalTime.Should().Be("21:00");
        body.Data.GracePeriodMinutes.Should().Be(20);
        body.Data.IsAutoAlertEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task Patch_WhenCalledTwice_UpdatesAndReturns200()
    {
        // Arrange
        var userId = Guid.NewGuid();
        AuthorizeAs(userId);

        await _client.PatchAsJsonAsync(Endpoint, new
        {
            dailyDeadlineLocalTime = "19:00",
            gracePeriodMinutes     = 15,
            reminderBeforeMinutes  = 30,
            autoAlertDelayMinutes  = 15,
            isMonitoringEnabled    = true,
            isAutoAlertEnabled     = true
        });

        // Act — gọi lần 2 để update
        var response = await _client.PatchAsJsonAsync(Endpoint, new
        {
            dailyDeadlineLocalTime = "23:00",
            gracePeriodMinutes     = 5,
            reminderBeforeMinutes  = 10,
            autoAlertDelayMinutes  = 3,
            isMonitoringEnabled    = false,
            isAutoAlertEnabled     = false
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<SafetySettingDto>>();
        body!.Success.Should().BeTrue();
        body.Data!.DailyDeadlineLocalTime.Should().Be("23:00");
        body.Data.GracePeriodMinutes.Should().Be(5);
        body.Data.IsMonitoringEnabled.Should().BeFalse();
        body.Data.IsAutoAlertEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task Patch_WhenDeadlineFormatInvalid_Returns400WithValidationError()
    {
        // Arrange
        AuthorizeAs(Guid.NewGuid());

        // Act
        var response = await _client.PatchAsJsonAsync(Endpoint, new
        {
            dailyDeadlineLocalTime = "9:00",   // thiếu leading zero — không khớp HH:mm
            gracePeriodMinutes     = 15,
            reminderBeforeMinutes  = 30,
            autoAlertDelayMinutes  = 15,
            isMonitoringEnabled    = true,
            isAutoAlertEnabled     = true
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        body!.Success.Should().BeFalse();
        body.Code.Should().Be(ErrorCodes.Validation.VALIDATION_ERROR);
        body.Errors.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Patch_WhenGracePeriodOutOfRange_Returns400WithValidationError()
    {
        // Arrange
        AuthorizeAs(Guid.NewGuid());

        // Act
        var response = await _client.PatchAsJsonAsync(Endpoint, new
        {
            dailyDeadlineLocalTime = "20:00",
            gracePeriodMinutes     = 200,   // > 120
            reminderBeforeMinutes  = 30,
            autoAlertDelayMinutes  = 15,
            isMonitoringEnabled    = true,
            isAutoAlertEnabled     = true
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        body!.Success.Should().BeFalse();
        body.Code.Should().Be(ErrorCodes.Validation.VALIDATION_ERROR);
    }

    [Fact]
    public async Task Patch_WhenAutoAlertDelayOutOfRange_Returns400WithValidationError()
    {
        // Arrange
        AuthorizeAs(Guid.NewGuid());

        // Act
        var response = await _client.PatchAsJsonAsync(Endpoint, new
        {
            dailyDeadlineLocalTime = "20:00",
            gracePeriodMinutes     = 15,
            reminderBeforeMinutes  = 30,
            autoAlertDelayMinutes  = 999,   // > 60
            isMonitoringEnabled    = true,
            isAutoAlertEnabled     = true
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        body!.Success.Should().BeFalse();
        body.Code.Should().Be(ErrorCodes.Validation.VALIDATION_ERROR);
    }

    // ===================== Helpers =====================

    private void AuthorizeAs(Guid userId)
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TokenHelper.GenerateUserToken(userId, "testuser"));
    }

    private static object ValidRequest() => new
    {
        dailyDeadlineLocalTime = "20:00",
        gracePeriodMinutes     = 15,
        reminderBeforeMinutes  = 30,
        autoAlertDelayMinutes  = 15,
        isMonitoringEnabled    = true,
        isAutoAlertEnabled     = true
    };
}
