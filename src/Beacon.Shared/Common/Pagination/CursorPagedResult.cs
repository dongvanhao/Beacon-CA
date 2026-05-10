namespace Beacon.Shared.Common.Pagination
{
    public class CursorPagedResult<T>
    {
        public List<T> Data { get; init; } = new();
        public CursorMeta Meta { get; init; } = new();
    }

    public class CursorPagedResult<T, TCursor> where TCursor : struct
    {
        public List<T> Data { get; init; } = new();
        public CursorMeta<TCursor> Meta { get; init; } = new();
    }

    public class CursorMeta
    {
        public DateTime? NextCursor { get; init; }
        public int Limit { get; init; }
        public bool HasMore { get; init; }
    }

    public class CursorMeta<TCursor> where TCursor : struct
    {
        public TCursor? NextCursor { get; init; }
        public int Limit { get; init; }
        public bool HasMore { get; init; }
    }
}
