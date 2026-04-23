using Beacon.Domain.Entities.Identity;
using Beacon.Infrashtructure.Presistence;
using Beacon.IntergrationTests.Common;
using Beacon.Shared.Common.Responses;
using Beacon.Shared.Constants;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Beacon.IntergrationTests.Identity;

public class ChangePasswordTests : IClassFixture<BeaconWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly BeaconWebApplicationFactory _factory;
    private const string Endpoint = "/api/v1/users/me/password";
    private const string CurrentPassword = "OldPass123!";
    private const string NewPassword = "NewPass456@";

    public ChangePasswordTests(BeaconWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ChangePassword_WithoutToken_Returns401()
    {
        var response = await _client.PatchAsJsonAsync(Endpoint, new
        {
            currentPassword = CurrentPassword,
            newPassword = NewPassword
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ChangePassword_HappyPath_Returns200()
    {
        // Arrange
        var (userId, _) = await SeedUserAsync();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TokenHelper.GenerateUserToken(userId, "testuser"));

        // Act
        var response = await _client.PatchAsJsonAsync(Endpoint, new
        {
            currentPassword = CurrentPassword,
            newPassword = NewPassword
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        body!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ChangePassword_HappyPath_AllRefreshTokensRevoked()
    {
        // Arrange
        var (userId, db) = await SeedUserAsync();
        var refreshToken = RefreshToken.Create(userId, "test-refresh-token", DateTime.UtcNow.AddDays(7));
        db.Set<RefreshToken>().Add(refreshToken);
        await db.SaveChangesAsync();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TokenHelper.GenerateUserToken(userId, "testuser"));

        // Act
        await _client.PatchAsJsonAsync(Endpoint, new
        {
            currentPassword = CurrentPassword,
            newPassword = NewPassword
        });

        // Assert
        db.ChangeTracker.Clear();
        var token = db.Set<RefreshToken>().Find(refreshToken.Id);
        token!.IsRevoked.Should().BeTrue();
    }

    [Fact]
    public async Task ChangePassword_WithWrongCurrentPassword_Returns401()
    {
        // Arrange
        var (userId, _) = await SeedUserAsync();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TokenHelper.GenerateUserToken(userId, "testuser"));

        // Act
        var response = await _client.PatchAsJsonAsync(Endpoint, new
        {
            currentPassword = "WrongPassword!1",
            newPassword = NewPassword
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        body!.Success.Should().BeFalse();
        body.Code.Should().Be(ErrorCodes.Identity.INVALID_CURRENT_PASSWORD);
    }

    [Fact]
    public async Task ChangePassword_WithWeakNewPassword_Returns400()
    {
        // Arrange
        var (userId, _) = await SeedUserAsync();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TokenHelper.GenerateUserToken(userId, "testuser"));

        // Act
        var response = await _client.PatchAsJsonAsync(Endpoint, new
        {
            currentPassword = CurrentPassword,
            newPassword = "weak"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        body!.Success.Should().BeFalse();
        body.Code.Should().Be(ErrorCodes.Validation.VALIDATION_ERROR);
    }

    [Fact]
    public async Task ChangePassword_WhenNewSameAsCurrent_Returns400()
    {
        // Arrange
        var (userId, _) = await SeedUserAsync();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TokenHelper.GenerateUserToken(userId, "testuser"));

        // Act
        var response = await _client.PatchAsJsonAsync(Endpoint, new
        {
            currentPassword = CurrentPassword,
            newPassword = CurrentPassword
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        body!.Success.Should().BeFalse();
        body.Code.Should().Be(ErrorCodes.Identity.NEW_PASSWORD_SAME_AS_OLD);
    }

    [Fact]
    public async Task ChangePassword_WhenUserInactive_Returns401()
    {
        // Arrange
        var (userId, db) = await SeedUserAsync();
        var user = db.Users.Find(userId)!;
        user.Deactivate();
        await db.SaveChangesAsync();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TokenHelper.GenerateUserToken(userId, "testuser"));

        // Act
        var response = await _client.PatchAsJsonAsync(Endpoint, new
        {
            currentPassword = CurrentPassword,
            newPassword = NewPassword
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        body!.Success.Should().BeFalse();
        body.Code.Should().Be(ErrorCodes.Identity.ACCOUNT_INACTIVE);
    }

    private async Task<(Guid UserId, AppDbContext Db)> SeedUserAsync()
    {
        var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();

        var user = User.Create(
            username: $"testuser_{Guid.NewGuid():N}",
            email: $"test_{Guid.NewGuid():N}@example.com",
            passwordHash: BCrypt.Net.BCrypt.HashPassword(CurrentPassword),
            familyName: "Test",
            givenName: "User");

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return (user.Id, db);
    }
}
