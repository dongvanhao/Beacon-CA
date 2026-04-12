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
            public const string EMAIL_ALREADY_EXISTS = "EMAIL_ALREADY_EXISTS";
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
    }

}

