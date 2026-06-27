using System.Net;
using System.Security.Claims;
using Beacon.Api.Logging;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Serilog.Events;

namespace Beacon.UnitTests.Logging;

public class RequestLoggingHelperTests
{
    // ── IsExcluded ────────────────────────────────────────────────────────────
    [Theory]
    [InlineData("/health")]
    [InlineData("/health/ready")]
    [InlineData("/hangfire")]
    [InlineData("/hangfire/jobs")]
    public void IsExcluded_ExcludedPath_ReturnsTrue(string path)
        => RequestLoggingHelper.IsExcluded(new PathString(path)).Should().BeTrue();

    [Theory]
    [InlineData("/api/v1/users")]
    [InlineData("/healthcheck")] // segment khác — KHÔNG được over-match "/health"
    [InlineData("/")]
    public void IsExcluded_ApiPath_ReturnsFalse(string path)
        => RequestLoggingHelper.IsExcluded(new PathString(path)).Should().BeFalse();

    // ── ResolveUserId ─────────────────────────────────────────────────────────
    [Fact]
    public void ResolveUserId_WithNameIdentifier_ReturnsGuid()
    {
        var id = Guid.NewGuid();
        var user = BuildPrincipal((ClaimTypes.NameIdentifier, id.ToString()));

        RequestLoggingHelper.ResolveUserId(user).Should().Be(id.ToString());
    }

    [Fact]
    public void ResolveUserId_WithOnlySubClaim_ReturnsGuid()
    {
        var id = Guid.NewGuid();
        var user = BuildPrincipal(("sub", id.ToString()));

        RequestLoggingHelper.ResolveUserId(user).Should().Be(id.ToString());
    }

    [Fact]
    public void ResolveUserId_Anonymous_ReturnsAnonymous()
    {
        RequestLoggingHelper.ResolveUserId(new ClaimsPrincipal()).Should().Be("anonymous");
        RequestLoggingHelper.ResolveUserId(null).Should().Be("anonymous");
    }

    // ── SanitizeQueryString ───────────────────────────────────────────────────
    [Fact]
    public void SanitizeQueryString_WithTokenParam_MasksValue()
    {
        var query = BuildQuery(("access_token", "abc123"), ("page", "2"));

        var result = RequestLoggingHelper.SanitizeQueryString(query);

        result.Should().Contain("access_token=***");
        result.Should().Contain("page=2");
    }

    [Fact]
    public void SanitizeQueryString_WithMonkeyParam_DoesNotMask()
    {
        // "monkey" chứa "key" nhưng KHÔNG khớp exact → không được mask
        var query = BuildQuery(("monkey", "george"));

        RequestLoggingHelper.SanitizeQueryString(query).Should().Contain("monkey=george");
    }

    [Fact]
    public void SanitizeQueryString_IsCaseInsensitive()
    {
        var query = BuildQuery(("Access_Token", "abc123"));

        RequestLoggingHelper.SanitizeQueryString(query).Should().Contain("Access_Token=***");
    }

    // ── MaskIp ────────────────────────────────────────────────────────────────
    [Fact]
    public void MaskIp_WhenEnabled_MasksLastOctet()
    {
        var ip = IPAddress.Parse("192.168.1.55");

        RequestLoggingHelper.MaskIp(ip, mask: true).Should().Be("192.168.1.0");
    }

    [Fact]
    public void MaskIp_WhenDisabled_ReturnsFull()
    {
        var ip = IPAddress.Parse("192.168.1.55");

        RequestLoggingHelper.MaskIp(ip, mask: false).Should().Be("192.168.1.55");
    }

    [Fact]
    public void MaskIp_Null_ReturnsNull()
        => RequestLoggingHelper.MaskIp(null, mask: true).Should().BeNull();

    // ── ResolveLevel ──────────────────────────────────────────────────────────
    [Fact]
    public void ResolveLevel_500_ReturnsError()
        => RequestLoggingHelper.ResolveLevel(500, isExcluded: false, hasException: false)
            .Should().Be(LogEventLevel.Error);

    [Fact]
    public void ResolveLevel_WithException_ReturnsError()
        => RequestLoggingHelper.ResolveLevel(200, isExcluded: false, hasException: true)
            .Should().Be(LogEventLevel.Error);

    [Fact]
    public void ResolveLevel_404_ReturnsWarning()
        => RequestLoggingHelper.ResolveLevel(404, isExcluded: false, hasException: false)
            .Should().Be(LogEventLevel.Warning);

    [Fact]
    public void ResolveLevel_200_ReturnsInformation()
        => RequestLoggingHelper.ResolveLevel(200, isExcluded: false, hasException: false)
            .Should().Be(LogEventLevel.Information);

    [Fact]
    public void ResolveLevel_ExcludedPath_ReturnsVerbose()
        => RequestLoggingHelper.ResolveLevel(200, isExcluded: true, hasException: false)
            .Should().Be(LogEventLevel.Verbose);

    // ── helpers ───────────────────────────────────────────────────────────────
    private static ClaimsPrincipal BuildPrincipal(params (string Type, string Value)[] claims)
    {
        var identity = new ClaimsIdentity(
            claims.Select(c => new Claim(c.Type, c.Value)),
            authenticationType: "TestAuth");
        return new ClaimsPrincipal(identity);
    }

    private static IQueryCollection BuildQuery(params (string Key, string Value)[] items)
        => new QueryCollection(items.ToDictionary(
            i => i.Key,
            i => new Microsoft.Extensions.Primitives.StringValues(i.Value)));
}
