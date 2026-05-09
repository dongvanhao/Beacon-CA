using Beacon.Domain.Common;
using Beacon.Domain.Enums.Identity;

namespace Beacon.Domain.Entities.Identity;

public class UserDeviceToken : AuditableEntity
{
    public Guid UserId { get; private set; }
    public string Token { get; private set; } = default!;
    public DevicePlatform Platform { get; private set; }
    public string? DeviceId { get; private set; }
    public string? DeviceName { get; private set; }
    public string? AppVersion { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime LastUsedAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }

    public User User { get; private set; } = default!;

    protected UserDeviceToken() { }

    public static UserDeviceToken Create(
        Guid userId, string token, DevicePlatform platform,
        string? deviceId = null, string? deviceName = null, string? appVersion = null)
        => new()
        {
            UserId = userId,
            Token = token,
            Platform = platform,
            DeviceId = deviceId,
            DeviceName = deviceName,
            AppVersion = appVersion,
            IsActive = true,
            LastUsedAtUtc = DateTime.UtcNow
        };

    public void UpdateOwner(Guid newUserId)
    {
        UserId = newUserId;
    }

    public void RecordUsage()
    {
        LastUsedAtUtc = DateTime.UtcNow;
        IsActive = true;
    }

    public void Revoke()
    {
        IsActive = false;
        RevokedAtUtc = DateTime.UtcNow;
    }

    public void MarkInvalid()
    {
        IsActive = false;
        RevokedAtUtc = DateTime.UtcNow;
    }
}
