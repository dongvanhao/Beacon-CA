using Beacon.Infrashtructure.Configuration;
using Beacon.Infrashtructure.DevSeed;
using Beacon.Shared.Common.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Beacon.Api.Controllers.Dev;

[ApiController]
[Route("api/v1/dev/test-data")]
public sealed class DevTestDataController(
    IWebHostEnvironment environment,
    IConfiguration configuration,
    DevTestDataSeeder seeder) : ControllerBase
{
    private const string ResetTokenHeader = "X-Dev-Seed-Token";

    [HttpPost("reset")]
    [AllowAnonymous]
    public async Task<IActionResult> Reset(CancellationToken ct)
    {
        if (!IsResetAvailable())
            return NotFound(ApiResponse<object>.FailureResponse(
                "Dev test data reset is unavailable.",
                "DEV_TEST_DATA_RESET_UNAVAILABLE"));

        var expectedToken = configuration["DevSeed:ResetToken"];
        if (string.IsNullOrWhiteSpace(expectedToken))
            return Unauthorized(ApiResponse<object>.FailureResponse(
                "Dev seed reset token is not configured.",
                "DEV_SEED_RESET_TOKEN_NOT_CONFIGURED"));

        var actualToken = Request.Headers[ResetTokenHeader].ToString();
        if (!string.Equals(actualToken, expectedToken, StringComparison.Ordinal))
            return Unauthorized(ApiResponse<object>.FailureResponse(
                "Invalid dev seed reset token.",
                "DEV_SEED_RESET_TOKEN_INVALID"));

        await seeder.ResetAsync(ct);

        return Ok(ApiResponse<object>.SuccessResponse(
            new
            {
                loginUsername = DevTestDataSeeder.SeedLoginUsername,
                loginEmail = DevTestDataSeeder.SeedLoginEmail
            },
            "Dev test data reset successfully."));
    }

    private bool IsResetAvailable()
        => environment.IsDevelopment()
           && DatabaseProviderOptions.IsInMemory(configuration)
           && IsEnabled("DevSeed:Enabled");

    private bool IsEnabled(string key)
        => bool.TryParse(configuration[key], out var enabled) && enabled;
}
