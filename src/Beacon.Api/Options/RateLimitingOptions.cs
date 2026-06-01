namespace Beacon.Api.Options;

public sealed class RateLimitingOptions
{
    public bool Enabled { get; set; } = true;
    public AuthRateLimitOptions Auth { get; set; } = new();
    public ApiRateLimitOptions Api { get; set; } = new();
    public GlobalRateLimitOptions Global { get; set; } = new();
}

public sealed class AuthRateLimitOptions
{
    public int LoginPermitLimit { get; set; } = 10;
    public int LoginWindowMinutes { get; set; } = 15;
    public int AdminLoginPermitLimit { get; set; } = 5;
    public int AdminLoginWindowMinutes { get; set; } = 15;
    public int RegisterPermitLimit { get; set; } = 5;
    public int RegisterWindowHours { get; set; } = 1;
    public int RefreshTokenPermitLimit { get; set; } = 30;
    public int RefreshTokenWindowMinutes { get; set; } = 15;
    public int CheckEmailPermitLimit { get; set; } = 20;
    public int CheckEmailWindowSeconds { get; set; } = 60;
    public int CheckPhonePermitLimit { get; set; } = 20;
    public int CheckPhoneWindowSeconds { get; set; } = 60;
}

public sealed class ApiRateLimitOptions
{
    public int AuthenticatedPermitLimit { get; set; } = 200;
    public int AuthenticatedWindowMinutes { get; set; } = 1;
    public int AuthenticatedBurst { get; set; } = 50;
    public int AdminPermitLimit { get; set; } = 500;
    public int AdminBurst { get; set; } = 100;
    public int UnauthenticatedPermitLimit { get; set; } = 60;
    public int UnauthenticatedWindowMinutes { get; set; } = 1;
}

public sealed class GlobalRateLimitOptions
{
    public int ConcurrencyLimit { get; set; } = 1000;
    public int QueueLimit { get; set; } = 0;
}
