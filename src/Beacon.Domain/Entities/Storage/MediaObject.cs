using Beacon.Domain.Common;
using Beacon.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Beacon.Domain.Entities.Storage
{
    public class MediaObject : SoftDeletableEntity
    {
        public Guid? UploadProviderByUserId { get; private set; }

        public StorageProvider StorageProvider { get; private set; } = StorageProvider.MinIO;
        public MediaAccessType AccessType { get; private set; } = MediaAccessType.Private;

        public string BucketName { get; private set; } = default!;
        public string ObjectKey { get; private set; } = default!;

        public string OriginalFileName { get; private set; } = default!;
        public string ContentType { get; private set; } = default!;
        public long FileSizeBytes { get; private set; }

        public string? ETag { get; private set; }
        public string? ChecksumSha256 { get; private set; }
        protected MediaObject() { }
    }
}
