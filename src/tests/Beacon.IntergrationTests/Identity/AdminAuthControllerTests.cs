using Beacon.Application.Features.Identity.Dtos;
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

public class AdminAuthControllerTests : IClassFixture<BeaconWebApplicationFactory>
{
    private const string MeEndpoint = "/api/v1/admin/auth/me";
    private const string RefreshTokenEndpoint = "/api/v1/admin/auth/refresh-token";

    private readonly HttpClient _client;
    private readonly BeaconWebApplicationFactory _factory;

    public AdminAuthControllerTests(BeaconWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync(MeEndpoint);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_WithUserToken_Returns403()
    {
        var userId = Guid.NewGuid();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TokenHelper.GenerateUserToken(userId, "testuser"));

        var response = await _client.GetAsync(MeEndpoint);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Me_WithAdminToken_ReturnsCurrentAdminProfile()
    {
        var (adminId, _) = await SeedAdminAsync();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TokenHelper.GenerateAdminToken(adminId, "admin"));

        var response = await _client.GetAsync(MeEndpoint);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AdminProfileDto>>();
        body!.Success.Should().BeTrue();
        body.Data!.AdminId.Should().Be(adminId);
        body.Data.Username.Should().Be("admin");
        body.Data.FullName.Should().Be("Admin User");
    }

    [Fact]
    public async Task Me_WithStaleAdminToken_Returns401()
    {
        var (adminId, _) = await SeedAdminAsync();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                TokenHelper.GenerateAdminToken(adminId, "admin", roles: ["SuperAdmin"]));

        var response = await _client.GetAsync(MeEndpoint);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RefreshToken_WithInvalidToken_Returns401()
    {
        var response = await _client.PostAsJsonAsync(RefreshTokenEndpoint, new
        {
            refreshToken = "invalid-token"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        body!.Success.Should().BeFalse();
        body.Code.Should().Be(ErrorCodes.Identity.ADMIN_TOKEN_INVALID);
    }

    [Fact]
    public async Task RefreshToken_WithValidToken_ReturnsNewTokensAndRevokesOldToken()
    {
        var (adminId, db) = await SeedAdminAsync();
        var oldRefreshToken = RefreshTokenAdmin.Create(
            adminId,
            "valid-admin-refresh-token",
            DateTime.UtcNow.AddDays(7));

        db.RefreshTokenAdmins.Add(oldRefreshToken);
        await db.SaveChangesAsync();

        var response = await _client.PostAsJsonAsync(RefreshTokenEndpoint, new
        {
            refreshToken = "valid-admin-refresh-token"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AdminAuthResponse>>();
        body!.Success.Should().BeTrue();
        body.Data!.AdminId.Should().Be(adminId);
        body.Data.AccessToken.Should().NotBeNullOrWhiteSpace();
        body.Data.RefreshToken.Should().NotBe("valid-admin-refresh-token");

        db.ChangeTracker.Clear();
        var revokedToken = db.RefreshTokenAdmins.Single(x => x.Token == "valid-admin-refresh-token");
        revokedToken.IsRevoked.Should().BeTrue();
        revokedToken.ReplacedByToken.Should().Be(body.Data.RefreshToken);

        db.RefreshTokenAdmins.Any(x => x.Token == body.Data.RefreshToken).Should().BeTrue();
    }

    private async Task<(Guid AdminId, AppDbContext Db)> SeedAdminAsync()
    {
        var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();

        var admin = Admin.Create(
            username: "admin",
            passwordHash: BCrypt.Net.BCrypt.HashPassword("Password123!"),
            fullName: "Admin User");

        db.Admins.Add(admin);
        await db.SaveChangesAsync();

        return (admin.Id, db);
    }
}
