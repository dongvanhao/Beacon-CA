using System.Text;
using System.Text.Json;

namespace Beacon.Application.Features.Posts.Helpers;

public static class FeedCursorHelper
{
    private record CursorData(DateTime CreatedAt, Guid Id);

    public static (DateTime? createdAt, Guid? id) Decode(string? cursor)
    {
        if (string.IsNullOrEmpty(cursor)) return (null, null);
        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            var data = JsonSerializer.Deserialize<CursorData>(json)!;
            return (data.CreatedAt, data.Id);
        }
        catch { return (null, null); }
    }

    public static string Encode(DateTime createdAt, Guid id)
    {
        var json = JsonSerializer.Serialize(new CursorData(createdAt, id));
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }
}
