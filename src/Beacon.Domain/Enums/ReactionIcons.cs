namespace Beacon.Domain.Enums;

public static class ReactionIcons
{
    public static readonly IReadOnlySet<string> Supported =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "heart",  // ❤️
            "haha",   // 😂
            "like",   // 👍
            "sad",    // 😢
            "wow",    // 😮
        };

    public static bool IsValid(string icon) => Supported.Contains(icon);
}
