using Beacon.Application.Features.Posts.Dtos;
using Beacon.Domain.Entities.Group;
using Beacon.Domain.Entities.Identity;
using Beacon.Domain.Entities.Posts;
using Beacon.Domain.Enums;
using Beacon.Infrashtructure.Presistence;
using Beacon.IntergrationTests.Common;
using Beacon.Shared.Common.Responses;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Beacon.IntergrationTests.Posts;

public class PostReactionsControllerTests : IClassFixture<BeaconWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly BeaconWebApplicationFactory _factory;

    public PostReactionsControllerTests(BeaconWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ─── 401 ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetReactions_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync($"/api/v1/posts/{Guid.NewGuid()}/reactions");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ─── 404 ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetReactions_WhenPostNotFound_Returns404()
    {
        var (userId, _) = await SeedUserAsync();
        AuthorizeAs(userId);

        var response = await _client.GetAsync($"/api/v1/posts/{Guid.NewGuid()}/reactions");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        body!.Success.Should().BeFalse();
        body.Code.Should().Be("POST_NOT_FOUND");
    }

    // ─── 403 ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetReactions_WhenPrivatePostAndNotOwner_ReturnsForbidden()
    {
        var (ownerId, db) = await SeedUserAsync();
        var (viewerId, _) = await SeedUserAsync();
        var post = await SeedPostAsync(db, ownerId, PostVisibility.Private);
        AuthorizeAs(viewerId);

        var response = await _client.GetAsync($"/api/v1/posts/{post.Id}/reactions");

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        body!.Success.Should().BeFalse();
        body.Code.Should().Be("POST_ACCESS_DENIED");
    }

    [Fact]
    public async Task GetReactions_WhenFriendsPostAndNotFriend_ReturnsForbidden()
    {
        var (ownerId, db) = await SeedUserAsync();
        var (viewerId, _) = await SeedUserAsync();
        var post = await SeedPostAsync(db, ownerId, PostVisibility.Friends);
        AuthorizeAs(viewerId);

        var response = await _client.GetAsync($"/api/v1/posts/{post.Id}/reactions");

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        body!.Success.Should().BeFalse();
        body.Code.Should().Be("POST_ACCESS_DENIED");
    }

    // ─── 400 ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetReactions_WithSeparatorInIcon_Returns400()
    {
        var (userId, db) = await SeedUserAsync();
        var post = await SeedPostAsync(db, userId, PostVisibility.Private);
        AuthorizeAs(userId);

        var response = await _client.GetAsync($"/api/v1/posts/{post.Id}/reactions?icon={Uri.EscapeDataString("heart - haha")}");

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        body!.Success.Should().BeFalse();
        body.Code.Should().Be("VALIDATION_ERROR");
    }

    [Fact]
    public async Task GetReactions_WithLimitOver100_Returns400()
    {
        var (userId, db) = await SeedUserAsync();
        var post = await SeedPostAsync(db, userId, PostVisibility.Private);
        AuthorizeAs(userId);

        var response = await _client.GetAsync($"/api/v1/posts/{post.Id}/reactions?limit=200");

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        body!.Success.Should().BeFalse();
        body.Code.Should().Be("VALIDATION_ERROR");
    }

    // ─── 200 Happy path ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetReactions_WhenOwnerNoReactions_ReturnsEmptyWithZeroSummary()
    {
        var (ownerId, db) = await SeedUserAsync();
        var post = await SeedPostAsync(db, ownerId, PostVisibility.Private);
        AuthorizeAs(ownerId);

        var response = await _client.GetAsync($"/api/v1/posts/{post.Id}/reactions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PostReactionListResponse>>();
        body!.Success.Should().BeTrue();
        body.Data!.Items.Should().BeEmpty();
        body.Data.HasMore.Should().BeFalse();
        body.Data.NextCursor.Should().BeNull();
        body.Data.Summary.TotalCount.Should().Be(0);
        body.Data.Summary.Icons.Should().BeEmpty();
    }

    [Fact]
    public async Task GetReactions_WhenOwnerHasReactions_ReturnsCorrectData()
    {
        var (ownerId, db) = await SeedUserAsync();
        var (reactorId, _) = await SeedUserAsync();
        var post = await SeedPostAsync(db, ownerId, PostVisibility.Private);
        await SeedReactionAsync(db, post.Id, reactorId, "heart");
        await SeedReactionAsync(db, post.Id, ownerId, "like");
        AuthorizeAs(ownerId);

        var response = await _client.GetAsync($"/api/v1/posts/{post.Id}/reactions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PostReactionListResponse>>();
        body!.Success.Should().BeTrue();
        body.Data!.Items.Should().HaveCount(2);
        body.Data.Items.Select(i => i.Icon).Should().BeSubsetOf(new[] { "heart", "like" });
        body.Data.Summary.TotalCount.Should().Be(2);
        body.Data.Summary.Icons["heart"].Should().Be(1);
        body.Data.Summary.Icons["like"].Should().Be(1);
    }

    [Fact]
    public async Task GetReactions_WhenFriendViewsFriendsPost_ReturnsOwnReaction()
    {
        var (ownerId, db) = await SeedUserAsync();
        var (friendId, _) = await SeedUserAsync();
        var post = await SeedPostAsync(db, ownerId, PostVisibility.Friends);
        await SeedFriendshipAsync(db, ownerId, friendId);
        await SeedReactionAsync(db, post.Id, friendId, "haha");
        AuthorizeAs(friendId);

        var response = await _client.GetAsync($"/api/v1/posts/{post.Id}/reactions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PostReactionListResponse>>();
        body!.Success.Should().BeTrue();
        body.Data!.Items.Should().HaveCount(1);
        body.Data.Items[0].Icon.Should().Be("haha");
        body.Data.Summary.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetReactions_WithIconFilter_ReturnsOnlyMatchingIcon()
    {
        var (ownerId, db) = await SeedUserAsync();
        var (u1, _) = await SeedUserAsync();
        var (u2, _) = await SeedUserAsync();
        var post = await SeedPostAsync(db, ownerId, PostVisibility.Private);
        await SeedReactionAsync(db, post.Id, u1, "heart");
        await SeedReactionAsync(db, post.Id, u2, "like");
        AuthorizeAs(ownerId);

        var response = await _client.GetAsync($"/api/v1/posts/{post.Id}/reactions?icon=heart");

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PostReactionListResponse>>();
        body!.Success.Should().BeTrue();
        body.Data!.Items.Should().HaveCount(1);
        body.Data.Items[0].Icon.Should().Be("heart");
        body.Data.Summary.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task GetReactions_Pagination_SecondPageReturnsDifferentItems()
    {
        var (ownerId, db) = await SeedUserAsync();
        var post = await SeedPostAsync(db, ownerId, PostVisibility.Private);

        for (var i = 0; i < 5; i++)
        {
            var (reactorId, _) = await SeedUserAsync();
            await SeedReactionAsync(db, post.Id, reactorId, "heart");
        }

        AuthorizeAs(ownerId);

        // Page 1 — limit=3
        var page1Response = await _client.GetAsync($"/api/v1/posts/{post.Id}/reactions?limit=3");
        var page1 = await page1Response.Content.ReadFromJsonAsync<ApiResponse<PostReactionListResponse>>();
        page1!.Success.Should().BeTrue();
        page1.Data!.Items.Should().HaveCount(3);
        page1.Data.HasMore.Should().BeTrue();
        page1.Data.NextCursor.Should().NotBeNull();

        var page2Response = await _client.GetAsync(
            $"/api/v1/posts/{post.Id}/reactions?limit=3&cursor={Uri.EscapeDataString(page1.Data.NextCursor!)}");
        var page2 = await page2Response.Content.ReadFromJsonAsync<ApiResponse<PostReactionListResponse>>();
        page2!.Success.Should().BeTrue();
        page2.Data!.Items.Should().HaveCount(2);
        page2.Data.HasMore.Should().BeFalse();

        var page1Ids = page1.Data.Items.Select(i => i.ReactionId).ToHashSet();
        var page2Ids = page2.Data.Items.Select(i => i.ReactionId).ToHashSet();
        page1Ids.Intersect(page2Ids).Should().BeEmpty();

        // Page 2 — using cursor from page 1
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private void AuthorizeAs(Guid userId)
        => _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TokenHelper.GenerateUserToken(userId, $"user_{userId:N}"));

    private async Task<(Guid UserId, AppDbContext Db)> SeedUserAsync()
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

    private static async Task<Post> SeedPostAsync(
        AppDbContext db, Guid ownerId, PostVisibility visibility)
    {
        var post = Post.Create(ownerId, Guid.NewGuid(), null, visibility);
        db.Posts.Add(post);
        await db.SaveChangesAsync();
        return post;
    }

    private static async Task SeedReactionAsync(
        AppDbContext db, Guid postId, Guid userId, string icon)
    {
        var reaction = PostReaction.Create(postId, userId, icon);
        db.PostReactions.Add(reaction);
        await db.SaveChangesAsync();
    }

    private static async Task SeedFriendshipAsync(AppDbContext db, Guid userA, Guid userB)
    {
        db.Friends.Add(Friend.Create(userA, userB));
        await db.SaveChangesAsync();
    }
}
