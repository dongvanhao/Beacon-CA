using Beacon.Application.Features.Group.Dtos;
using Beacon.Domain.Entities.Group;
using Beacon.Domain.Entities.Identity;
using Beacon.Domain.Enums.Group;
using Beacon.Infrashtructure.Presistence;
using Beacon.IntergrationTests.Common;
using Beacon.Shared.Common.Responses;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Beacon.IntergrationTests.Group;

public class NotificationsControllerTests : IClassFixture<BeaconWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly BeaconWebApplicationFactory _factory;

    public NotificationsControllerTests(BeaconWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ─── 401 ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task List_ShouldReturn401_WhenNotAuthenticated()
    {
        var response = await _client.GetAsync("/api/v1/notifications");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ─── GET / ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task List_ShouldReturn200_WithEmptyList()
    {
        var userId = await SeedUserAsync();
        AuthorizeAs(userId);

        var response = await _client.GetAsync("/api/v1/notifications");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<NotificationListResponse>>();
        body!.Success.Should().BeTrue();
        body.Data!.Items.Should().BeEmpty();
        body.Data.UnreadCount.Should().Be(0);
        body.Data.HasNextPage.Should().BeFalse();
    }

    [Fact]
    public async Task List_ShouldReturn400_WhenLimitOutOfRange()
    {
        var userId = await SeedUserAsync();
        AuthorizeAs(userId);

        var response = await _client.GetAsync("/api/v1/notifications?limit=100");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        body!.Code.Should().Be("VALIDATION_ERROR");
    }

    // ─── PATCH /{id}/read ─────────────────────────────────────────────────────

    [Fact]
    public async Task MarkRead_ShouldReturn200_AndDecreaseUnreadCount()
    {
        var (userId, db) = await SeedUserWithDbAsync();
        var notification = Notification.Create(userId, NotificationType.FriendRequest, "Test", "Body");
        db.Notifications.Add(notification);
        await db.SaveChangesAsync();
        AuthorizeAs(userId);

        var response = await _client.PatchAsync($"/api/v1/notifications/{notification.Id}/read", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<MarkReadResponse>>();
        body!.Success.Should().BeTrue();
        body.Data!.UnreadCount.Should().Be(0);
    }

    [Fact]
    public async Task MarkRead_ShouldReturn404_WhenNotFound()
    {
        var userId = await SeedUserAsync();
        AuthorizeAs(userId);
        var nonExistentId = Guid.NewGuid();

        var response = await _client.PatchAsync($"/api/v1/notifications/{nonExistentId}/read", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        body!.Code.Should().Be("NOTIFICATION_NOT_FOUND");
    }

    [Fact]
    public async Task MarkRead_ShouldReturn403_WhenWrongOwner()
    {
        var (ownerId, db) = await SeedUserWithDbAsync();
        var notification = Notification.Create(ownerId, NotificationType.FriendRequest, "Test", "Body");
        db.Notifications.Add(notification);
        await db.SaveChangesAsync();

        var requesterId = await SeedUserAsync();
        AuthorizeAs(requesterId);

        var response = await _client.PatchAsync($"/api/v1/notifications/{notification.Id}/read", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        body!.Code.Should().Be("NOTIFICATION_FORBIDDEN");
    }

    // ─── PATCH /read-all ──────────────────────────────────────────────────────

    [Fact]
    public async Task MarkAllRead_ShouldReturn200_WithZeroUnreadCount()
    {
        var (userId, db) = await SeedUserWithDbAsync();
        db.Notifications.Add(Notification.Create(userId, NotificationType.FriendRequest, "T1", "B1"));
        db.Notifications.Add(Notification.Create(userId, NotificationType.FriendAccepted, "T2", "B2"));
        await db.SaveChangesAsync();
        AuthorizeAs(userId);

        var response = await _client.PatchAsync("/api/v1/notifications/read-all", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<MarkReadResponse>>();
        body!.Success.Should().BeTrue();
        body.Data!.UnreadCount.Should().Be(0);
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
}
