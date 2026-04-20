using Beacon.Domain.Common;
using Beacon.Domain.Enums;

namespace Beacon.Domain.Entities.Storage
{
    public class MediaObject : SoftDeletableEntity
    {
        public Guid? UploadProviderByUserId { get; private set; }

        public StorageProvider StorageProvider { get; private set; } = StorageProvider.MinIO;
        public MediaAccessType AccessType { get; private set; } = MediaAccessType.Private;
        public MediaType MediaType { get; private set; } = MediaType.Image;

        public string BucketName { get; private set; } = default!;
        public string ObjectKey { get; private set; } = default!;
        public string? ThumbnailObjectKey { get; private set; }

        public string OriginalFileName { get; private set; } = default!;
        public string ContentType { get; private set; } = default!;
        public long FileSizeBytes { get; private set; }

        public int? Width { get; private set; }
        public int? Height { get; private set; }

        public string? ETag { get; private set; }
        public string? ChecksumSha256 { get; private set; }

        protected MediaObject() { }

        public static MediaObject Create(
            string bucketName,
            string objectKey,
            string originalFileName,
            string contentType,
            long fileSizeBytes,
            MediaType mediaType,
            StorageProvider storageProvider = StorageProvider.MinIO,
            MediaAccessType accessType = MediaAccessType.Private,
            Guid? uploadedByUserId = null,
            string? thumbnailObjectKey = null,
            int? width = null,
            int? height = null,
            string? etag = null,
            string? checksumSha256 = null)
            => new()
            {
                BucketName = bucketName,
                ObjectKey = objectKey,
                OriginalFileName = originalFileName,
                ContentType = contentType,
                FileSizeBytes = fileSizeBytes,
                MediaType = mediaType,
                StorageProvider = storageProvider,
                AccessType = accessType,
                UploadProviderByUserId = uploadedByUserId,
                ThumbnailObjectKey = thumbnailObjectKey,
                Width = width,
                Height = height,
                ETag = etag,
                ChecksumSha256 = checksumSha256
            };

        public void AttachThumbnail(string thumbnailObjectKey)
            => ThumbnailObjectKey = thumbnailObjectKey;

        public void SetDimensions(int width, int height)
        {
            Width = width;
            Height = height;
        }
    }
}
