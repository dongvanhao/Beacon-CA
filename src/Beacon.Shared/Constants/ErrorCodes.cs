using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Beacon.Shared.Constants
{
    public static class ErrorCodes
    {
        public static class Identity
        {
            public const string USER_NOT_FOUND = "USER_NOT_FOUND";
            public const string USERNAME_ALREADY_EXISTS = "USERNAME_ALREADY_EXISTS";
            public const string INVALID_CREDENTIALS = "INVALID_CREDENTIALS";
            public const string TOKEN_EXPIRED = "TOKEN_EXPIRED";
            public const string TOKEN_INVALID = "TOKEN_INVALID";
            public const string ACCOUNT_INACTIVE = "ACCOUNT_INACTIVE";

            public const string ADMIN_NOT_FOUND = "ADMIN_NOT_FOUND";
            public const string ADMIN_INACTIVE = "ADMIN_INACTIVE";
            public const string ADMIN_TOKEN_INVALID = "ADMIN_TOKEN_INVALID";
        }

        public static class Safety
        {
            public const string RECORD_NOT_FOUND = "SAFETY_RECORD_NOT_FOUND";
            public const string ALREADY_CHECKED_IN = "ALREADY_CHECKED_IN";
            public const string INCIDENT_NOT_FOUND = "INCIDENT_NOT_FOUND";
        }

        public static class Checkin
        {
            public const string CHECKIN_NOT_FOUND = "CHECKIN_NOT_FOUND";
            public const string CHECKIN_ALREADY_EXISTS = "CHECKIN_ALREADY_EXISTS";
        }

        public static class Storage
        {
            public const string MEDIA_NOT_FOUND = "MEDIA_NOT_FOUND";
            public const string UPLOAD_FAILED = "UPLOAD_FAILED";
        }

        /// <summary>
        /// Error codes cho health check endpoints (/health, /health/live, /health/ready, /health/db, /health/minio).
        /// Mỗi code map 1-1 với 1 loại lỗi cụ thể để team có thể filter alert/log.
        /// </summary>
        public static class HealthCheck
        {
            /// <summary>Có từ 2 services trở lên bị fail trong cùng một lần check.</summary>
            public const string MULTIPLE_FAILURES = "HEALTH_CHECK_MULTIPLE_FAILURES";

            /// <summary>SQL Server không kết nối được (timeout, network error, credential sai).</summary>
            public const string DATABASE_UNREACHABLE = "DATABASE_UNREACHABLE";

            /// <summary>MinIO chưa được cấu hình (thiếu key <c>MinIO:Endpoint</c> trong appsettings). Trạng thái: Degraded.</summary>
            public const string MINIO_NOT_CONFIGURED = "MINIO_NOT_CONFIGURED";

            /// <summary>MinIO đã config nhưng không thể reach (connection refused, DNS lookup fail, timeout).</summary>
            public const string MINIO_UNREACHABLE = "MINIO_UNREACHABLE";

            /// <summary>MinIO reach được nhưng trả về HTTP status code khác 2xx.</summary>
            public const string MINIO_BAD_STATUS = "MINIO_BAD_STATUS";

            /// <summary>Backend process không còn sống — thường không bao giờ được trả về vì nếu app die thì endpoint không response.</summary>
            public const string BACKEND_UNHEALTHY = "BACKEND_UNHEALTHY";
        }
    }

}

