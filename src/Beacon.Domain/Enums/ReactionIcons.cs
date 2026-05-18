namespace Beacon.Domain.Enums;

public static class ReactionIcons
{
    public const string Separator = " - ";
    public const int MaxIconsPerUser = 3;
    public const int MaxIconLength = 64;
    public const int MaxStoredLength = 198;

    public static bool IsValid(string icon)
        => !string.IsNullOrWhiteSpace(icon)
           && icon.Length <= MaxIconLength
           && !icon.Contains(Separator);

    public static IReadOnlyList<string> Split(string icons)
        => string.IsNullOrWhiteSpace(icons)
            ? Array.Empty<string>()
            : icons.Split(new[] { Separator }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public static string Append(string existingIcons, string newIcon)
    {
        var icons = Split(existingIcons).ToList();
        icons.Add(newIcon);

        if (icons.Count > MaxIconsPerUser)
            icons = icons.TakeLast(MaxIconsPerUser).ToList();

        return string.Join(Separator, icons);
    }
}
