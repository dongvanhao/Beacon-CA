using Beacon.Application.Features.Checkins.Dtos;
using Beacon.Domain.Entities.Identity;
using Beacon.Domain.Entities.Safety;
using Beacon.Infrashtructure.Presistence;
using Beacon.IntergrationTests.Common;
using Beacon.Shared.Common.Responses;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Beacon.IntergrationTests.Checkins;

public class CheckinsControllerTests : IClassFixture<BeaconWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly BeaconWebApplicationFactory _factory;
    private const string Endpoint = "/api/v1/checkins/today-status";

    public CheckinsControllerTests(BeaconWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ─── 401 ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTodayStatus_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync(Endpoint);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ─── Pending: chưa có record hôm nay ────────────────────────────────────

    [Fact]
    public async Task GetTodayStatus_WhenNoRecord_ReturnsPendingWithRemainingSeconds()
    {
        var userId = await SeedUserAsync();
        AuthorizeAs(userId);

        var response = await _client.GetAsync(Endpoint);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<TodayCheckinStatusDto>>();
        body!.Success.Should().BeTrue();
        body.Data!.HasCheckedIn.Should().BeFalse();
        body.Data.Status.Should().BeOneOf("Pending", "Overdue");
        body.Data.RemainingSeconds.Should().NotBeNull();
        body.Data.CheckedInAtUtc.Should().BeNull();
        body.Data.Streak.Should().Be(0);
    }

    // ─── CheckedIn ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTodayStatus_WhenAlreadyCheckedIn_ReturnsCheckedIn()
    {
        var (userId, db) = await SeedUserWithDbAsync();
        await SeedCheckedInRecordAsync(db, userId);
        AuthorizeAs(userId);

        var response = await _client.GetAsync(Endpoint);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<TodayCheckinStatusDto>>();
        body!.Success.Should().BeTrue();
        body.Data!.HasCheckedIn.Should().BeTrue();
        body.Data.Status.Should().Be("CheckedIn");
        body.Data.RemainingSeconds.Should().BeNull();
        body.Data.CheckedInAtUtc.Should().NotBeNull();
        body.Data.Streak.Should().BeGreaterThanOrEqualTo(0);
    }

    // ─── Overdue: qua deadline, chưa checkin ────────────────────────────────

    [Fact]
    public async Task GetTodayStatus_WhenPastDeadlineNotCheckedIn_ReturnsOverdue()
    {
        var (userId, db) = await SeedUserWithDbAsync();
        await SeedPendingRecordWithPastDeadlineAsync(db, userId);
        AuthorizeAs(userId);

        var response = await _client.GetAsync(Endpoint);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<TodayCheckinStatusDto>>();
        body!.Success.Should().BeTrue();
        body.Data!.HasCheckedIn.Should().BeFalse();
        body.Data.Status.Should().Be("Overdue");
        body.Data.RemainingSeconds.Should().BeLessThan(0);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private void AuthorizeAs(Guid userId)
        => _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TokenHelper.GenerateUserToken(userId, "testuser"));

    private async Task<Guid> SeedUserAsync()
    {
        var (userId, _) = await SeedUserWithDbAsync();
        return userId;
    }

    private async Task<(Guid UserId, AppDbContext Db)> SeedUserWithDbAsync()
    {
        var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();

        var user = User.Create(
            username: $"user_{Guid.NewGuid():N}",
            email: $"{Guid.NewGuid():N}@test.com",
            passwordHash: BCrypt.Net.BCrypt.HashPassword("Pass123!"),
            familyName: "Test",
            givenName: "User");

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return (user.Id, db);
    }

    private static readonly TimeZoneInfo VnTz =
        TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");

    private static DateOnly TodayVn =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VnTz));

    private static async Task SeedCheckedInRecordAsync(AppDbContext db, Guid userId)
    {
        var deadline = DateTime.UtcNow.Date.AddHours(23).AddMinutes(59);
        var record = DailySafetyRecord.Create(userId, TodayVn, deadline);
        record.MarkCheckedIn(DateTime.UtcNow.AddHours(-1));
        db.Set<DailySafetyRecord>().Add(record);
        await db.SaveChangesAsync();
    }

    private static async Task SeedPendingRecordWithPastDeadlineAsync(AppDbContext db, Guid userId)
    {
        var pastDeadline = DateTime.UtcNow.AddHours(-2);
        var record = DailySafetyRecord.Create(userId, TodayVn, pastDeadline);
        db.Set<DailySafetyRecord>().Add(record);
        await db.SaveChangesAsync();
    }
}
