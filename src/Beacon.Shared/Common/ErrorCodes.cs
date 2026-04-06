namespace Beacon.Shared.Common
{
    public static class ErrorCodes
    {
        public const string ValidationError = "VALIDATION_ERROR";
        public const string Unauthorized = "UNAUTHORIZED";
        public const string Forbidden = "FORBIDDEN";
        public const string NotFound = "NOT_FOUND";
        public const string Conflict = "CONFLICT";

        public const string DatabaseUnavailable = "DATABASE_UNAVAILABLE";
        public const string DatabaseTimeout = "DATABASE_TIMEOUT";
        public const string DatabaseException = "DATABASE_EXCEPTION";
    }
}
