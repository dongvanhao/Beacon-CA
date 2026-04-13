namespace Beacon.Application.Features.Identity.Dtos;

public class RegisterDeviceRequest
{
    /// <summary>FCM token (Android) hoặc APNs token (iOS) để gửi push notification.</summary>
    public string DeviceToken { get; set; } = default!;
}
