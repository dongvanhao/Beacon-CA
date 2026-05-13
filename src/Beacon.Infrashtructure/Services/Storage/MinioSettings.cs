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
    /// Endpoint công khai dùng để ký và trả presigned URL về client.
    /// Bắt buộc cấu hình bằng host mà client thật truy cập được.
    /// Ví dụ: "http://192.168.1.139:9000" (LAN), "http://10.0.2.2:9000" (Android emulator)
    /// hoặc "https://cdn.yourdomain.com" (production).
    /// </summary>
    public string? PublicEndpoint { get; init; }

    public string BucketName { get; init; } = "beacon-media";

    public string AccessKey { get; init; } = string.Empty;

    public string SecretKey { get; init; } = string.Empty;

    public bool UseSSL { get; init; } = false;

    /// <summary>Thời gian hiệu lực của presigned URL (giây). Mặc định 900s = 15 phút.</summary>
    public int PresignedUrlExpirySeconds { get; init; } = 900;

    /// <summary>
    /// Trả về public endpoint đã chuẩn hoá (bỏ trailing slash).
    /// Không fallback về Endpoint vì Endpoint có thể là host nội bộ như minio/localhost.
    /// </summary>
    public string GetRequiredPublicEndpoint()
    {
        if (string.IsNullOrWhiteSpace(PublicEndpoint))
            throw new InvalidOperationException(
                "MinIO:PublicEndpoint is required for media URLs returned to clients. " +
                "Set it to a reachable public/LAN endpoint, for example http://192.168.1.139:9000.");

        var endpoint = PublicEndpoint.Trim().TrimEnd('/');
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            string.IsNullOrWhiteSpace(uri.Host))
        {
            throw new InvalidOperationException(
                "MinIO:PublicEndpoint must be an absolute HTTP/HTTPS URL, for example http://192.168.1.139:9000.");
        }

        return endpoint;
    }
}
