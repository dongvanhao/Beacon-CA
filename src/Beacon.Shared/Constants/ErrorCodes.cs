using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Beacon.Shared.Constants
{
    public static class ErrorCodes
    {
        public static class Validation
        {
            public const string VALIDATION_ERROR = "VALIDATION_ERROR";
        }

        public static class Identity
        {
            public const string USER_NOT_FOUND = "USER_NOT_FOUND";
            public const string USERNAME_ALREADY_EXISTS = "USERNAME_ALREADY_EXISTS";
            public const string EMAIL_ALREADY_EXISTS = "EMAIL_ALREADY_EXISTS";
            public const string PHONE_ALREADY_EXISTS = "PHONE_ALREADY_EXISTS";
            public const string PHONE_ALREADY_IN_USE = "PHONE_ALREADY_IN_USE";
            public const string EMAIL_ALREADY_IN_USE = "EMAIL_ALREADY_IN_USE";
            public const string INVALID_CREDENTIALS = "INVALID_CREDENTIALS";
            public const string TOKEN_EXPIRED = "TOKEN_EXPIRED";
            public const string TOKEN_INVALID = "TOKEN_INVALID";
            public const string ACCOUNT_INACTIVE = "ACCOUNT_INACTIVE";

            public const string ADMIN_NOT_FOUND = "ADMIN_NOT_FOUND";
            public const string ADMIN_INACTIVE = "ADMIN_INACTIVE";
            public const string ADMIN_TOKEN_INVALID = "ADMIN_TOKEN_INVALID";

            public const string INVALID_CURRENT_PASSWORD = "INVALID_CURRENT_PASSWORD";
            public const string NEW_PASSWORD_SAME_AS_OLD = "NEW_PASSWORD_SAME_AS_OLD";
        }

        public static class Settings
        {
            public const string SAFETY_SETTING_NOT_FOUND = "SAFETY_SETTING_NOT_FOUND";
        }

        public static class Safety
        {
            public const string RECORD_NOT_FOUND = "SAFETY_RECORD_NOT_FOUND";
            public const string ALREADY_CHECKED_IN = "ALREADY_CHECKED_IN";
            public const string INCIDENT_NOT_FOUND = "INCIDENT_NOT_FOUND";

            public const string EMERGENCY_CONTACT_NOT_FOUND = "EMERGENCY_CONTACT_NOT_FOUND";
            public const string EMERGENCY_CONTACT_LIMIT_EXCEEDED = "EMERGENCY_CONTACT_LIMIT_EXCEEDED";
            public const string EMERGENCY_CONTACT_FORBIDDEN = "EMERGENCY_CONTACT_FORBIDDEN";
        }

        public static class Checkin
        {
            public const string CHECKIN_NOT_FOUND = "CHECKIN_NOT_FOUND";
            public const string CHECKIN_ALREADY_EXISTS = "CHECKIN_ALREADY_EXISTS";
        }

        public static class Storage
        {
            public const string MEDIA_NOT_FOUND = "MEDIA_NOT_FOUND";
            public const string MEDIA_FORBIDDEN = "MEDIA_FORBIDDEN";
            public const string UPLOAD_FAILED = "UPLOAD_FAILED";
            public const string INVALID_FILE_TYPE = "INVALID_FILE_TYPE";
            public const string FILE_TOO_LARGE = "FILE_TOO_LARGE";
            public const string FILE_EMPTY = "FILE_EMPTY";
            public const string STORAGE_UNAVAILABLE = "STORAGE_UNAVAILABLE";
        }

        /// <summary>
        /// Error codes cho health check endpoints (/health, /health/live, /health/ready, /health/db, /health/minio).
        /// Mỗi code map 1-1 với 1 loại lỗi cụ thể để team có thể filter alert/log.
        /// </summary>
        public static class Friend
        {
            public const string SELF_FRIEND_REQUEST        = "SELF_FRIEND_REQUEST";
            public const string FRIEND_REQUEST_DUPLICATE   = "FRIEND_REQUEST_DUPLICATE";
            public const string ALREADY_FRIENDS            = "ALREADY_FRIENDS";
            public const string FRIEND_REQUEST_NOT_FOUND   = "FRIEND_REQUEST_NOT_FOUND";
            public const string FRIEND_REQUEST_NOT_PENDING = "FRIEND_REQUEST_NOT_PENDING";
            public const string FRIEND_REQUEST_FORBIDDEN   = "FRIEND_REQUEST_FORBIDDEN";
            public const string FRIEND_NOT_FOUND           = "FRIEND_NOT_FOUND";
        }

        public static class Notification
        {
            public const string NOTIFICATION_NOT_FOUND = "NOTIFICATION_NOT_FOUND";
            public const string NOTIFICATION_FORBIDDEN  = "NOTIFICATION_FORBIDDEN";
        }

        public static class Messaging
        {
            public const string MESSAGE_GROUP_NOT_FOUND = "MESSAGE_GROUP_NOT_FOUND";
            public const string MESSAGE_GROUP_FORBIDDEN = "MESSAGE_GROUP_FORBIDDEN";
            public const string GROUP_MEMBER_ALREADY_EXISTS = "GROUP_MEMBER_ALREADY_EXISTS";
            public const string GROUP_MEMBER_NOT_FOUND = "GROUP_MEMBER_NOT_FOUND";
            public const string GROUP_MEMBER_NOT_PENDING = "GROUP_MEMBER_NOT_PENDING";
            public const string MESSAGE_NOT_FOUND = "MESSAGE_NOT_FOUND";
        }

        public static class Post
        {
            public const string POST_NOT_FOUND         = "POST_NOT_FOUND";
            public const string POST_ACCESS_DENIED     = "POST_ACCESS_DENIED";
            public const string POST_UPDATE_DENIED     = "POST_UPDATE_DENIED";
            public const string POST_DELETE_DENIED     = "POST_DELETE_DENIED";
            public const string INVALID_VISIBILITY     = "INVALID_VISIBILITY";
            public const string MEDIA_NOT_READY        = "MEDIA_NOT_READY";
            public const string MEDIA_ACCESS_DENIED    = "MEDIA_ACCESS_DENIED";
            public const string UNSUPPORTED_MEDIA_TYPE = "UNSUPPORTED_MEDIA_TYPE";
            public const string INVALID_VIDEO_DURATION = "INVALID_VIDEO_DURATION";
        }

        public static class Reaction
        {
            public const string INVALID_REACTION_ICON = "INVALID_REACTION_ICON";
            public const string REACTION_CONFLICT     = "REACTION_CONFLICT";
        }

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

