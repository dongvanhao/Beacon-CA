namespace Beacon.Infrashtructure.Services.Storage;

/// <summary>
/// Tập trung toàn bộ cấu hình MinIO vào một chỗ.
/// Bind từ section "MinIO" trong appsettings.
/// </summary>
public sealed class MinioSettings
{
    public const string SectionName = "MinIO";

    /// <summary>
    /// Endpoint nội bộ (dùng để SDK kết nối upload/delete).
    /// Ví dụ: "http://minio:9000" (trong Docker) hoặc "http://localhost:9000" (dev local).
    /// </summary>
    public string Endpoint { get; init; } = "http://localhost:9000";

    /// <summary>
    /// Endpoint công khai dùng để rewrite host trong presigned URL trả về client.
    /// Nếu để trống, sẽ fallback về Endpoint.
    /// Ví dụ: "http://localhost:9000" (dev) hoặc "https://cdn.yourdomain.com" (production).
    /// </summary>
    public string? PublicEndpoint { get; init; }

    public string BucketName { get; init; } = "beacon-media";

    public string AccessKey { get; init; } = string.Empty;

    public string SecretKey { get; init; } = string.Empty;

    public bool UseSSL { get; init; } = false;

    /// <summary>Thời gian hiệu lực của presigned URL (giây). Mặc định 900s = 15 phút.</summary>
    public int PresignedUrlExpirySeconds { get; init; } = 900;

    /// <summary>
    /// Trả về public endpoint đã chuẩn hoá (bỏ trailing slash, không có scheme cho MinIO SDK).
    /// Ưu tiên: PublicEndpoint → Endpoint.
    /// </summary>
    public string GetPublicEndpoint() =>
        (string.IsNullOrWhiteSpace(PublicEndpoint) ? Endpoint : PublicEndpoint)
            .TrimEnd('/');
}
