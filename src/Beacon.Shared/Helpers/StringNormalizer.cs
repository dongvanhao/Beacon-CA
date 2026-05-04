using System.Globalization;
using System.Text;

namespace Beacon.Shared.Helpers;

/// <summary>
/// Tiện ích chuẩn hoá chuỗi: bỏ dấu tiếng Việt để hỗ trợ tìm kiếm không phân biệt dấu.
/// </summary>
public static class StringNormalizer
{
    // ──────────────────────────────────────────────────────────────────────────
    // Bảng ánh xạ tường minh toàn bộ ký tự tiếng Việt đặc biệt → ASCII.
    //
    // Tại sao cần bảng này?
    //   • Đ/đ (U+0110/U+0111): KHÔNG phân giải được qua NFD — phải handle thủ công.
    //   • Các ký tự còn lại (ă,â,ê,ô,ơ,ư + dấu thanh): NFD decomposes đúng.
    //     Bảng dưới đây bổ sung explicit pre-replace để code tự-documented
    //     và không phụ thuộc hoàn toàn vào behaviour của NFD runtime.
    // ──────────────────────────────────────────────────────────────────────────
    private static readonly (char From, char To)[] VietnameseMap =
    [
        // ── Đ/đ: KHÔNG phân giải qua NFD, BẮT BUỘC phải explicit ──────────
        ('Đ', 'D'), ('đ', 'd'),

        // ── Nhóm A ──────────────────────────────────────────────────────────
        // Ă/ă + 5 dấu thanh
        ('Ắ', 'A'), ('ắ', 'a'), ('Ặ', 'A'), ('ặ', 'a'),
        ('Ằ', 'A'), ('ằ', 'a'), ('Ẳ', 'A'), ('ẳ', 'a'),
        ('Ẵ', 'A'), ('ẵ', 'a'), ('Ă', 'A'), ('ă', 'a'),
        // Â/â + 5 dấu thanh
        ('Ấ', 'A'), ('ấ', 'a'), ('Ậ', 'A'), ('ậ', 'a'),
        ('Ầ', 'A'), ('ầ', 'a'), ('Ẩ', 'A'), ('ẩ', 'a'),
        ('Ẫ', 'A'), ('ẫ', 'a'), ('Â', 'A'), ('â', 'a'),
        // A + 5 dấu thanh
        ('Á', 'A'), ('á', 'a'), ('À', 'A'), ('à', 'a'),
        ('Ả', 'A'), ('ả', 'a'), ('Ã', 'A'), ('ã', 'a'),
        ('Ạ', 'A'), ('ạ', 'a'),

        // ── Nhóm E ──────────────────────────────────────────────────────────
        // Ê/ê + 5 dấu thanh
        ('Ế', 'E'), ('ế', 'e'), ('Ệ', 'E'), ('ệ', 'e'),
        ('Ề', 'E'), ('ề', 'e'), ('Ể', 'E'), ('ể', 'e'),
        ('Ễ', 'E'), ('ễ', 'e'), ('Ê', 'E'), ('ê', 'e'),
        // E + 5 dấu thanh
        ('É', 'E'), ('é', 'e'), ('È', 'E'), ('è', 'e'),
        ('Ẻ', 'E'), ('ẻ', 'e'), ('Ẽ', 'E'), ('ẽ', 'e'),
        ('Ẹ', 'E'), ('ẹ', 'e'),

        // ── Nhóm I ──────────────────────────────────────────────────────────
        ('Í', 'I'), ('í', 'i'), ('Ì', 'I'), ('ì', 'i'),
        ('Ỉ', 'I'), ('ỉ', 'i'), ('Ĩ', 'I'), ('ĩ', 'i'),
        ('Ị', 'I'), ('ị', 'i'),

        // ── Nhóm O ──────────────────────────────────────────────────────────
        // Ô/ô + 5 dấu thanh
        ('Ố', 'O'), ('ố', 'o'), ('Ộ', 'O'), ('ộ', 'o'),
        ('Ồ', 'O'), ('ồ', 'o'), ('Ổ', 'O'), ('ổ', 'o'),
        ('Ỗ', 'O'), ('ỗ', 'o'), ('Ô', 'O'), ('ô', 'o'),
        // Ơ/ơ + 5 dấu thanh
        ('Ớ', 'O'), ('ớ', 'o'), ('Ợ', 'O'), ('ợ', 'o'),
        ('Ờ', 'O'), ('ờ', 'o'), ('Ở', 'O'), ('ở', 'o'),
        ('Ỡ', 'O'), ('ỡ', 'o'), ('Ơ', 'O'), ('ơ', 'o'),
        // O + 5 dấu thanh
        ('Ó', 'O'), ('ó', 'o'), ('Ò', 'O'), ('ò', 'o'),
        ('Ỏ', 'O'), ('ỏ', 'o'), ('Õ', 'O'), ('õ', 'o'),
        ('Ọ', 'O'), ('ọ', 'o'),

        // ── Nhóm U ──────────────────────────────────────────────────────────
        // Ư/ư + 5 dấu thanh
        ('Ứ', 'U'), ('ứ', 'u'), ('Ự', 'U'), ('ự', 'u'),
        ('Ừ', 'U'), ('ừ', 'u'), ('Ử', 'U'), ('ử', 'u'),
        ('Ữ', 'U'), ('ữ', 'u'), ('Ư', 'U'), ('ư', 'u'),
        // U + 5 dấu thanh
        ('Ú', 'U'), ('ú', 'u'), ('Ù', 'U'), ('ù', 'u'),
        ('Ủ', 'U'), ('ủ', 'u'), ('Ũ', 'U'), ('ũ', 'u'),
        ('Ụ', 'U'), ('ụ', 'u'),

        // ── Nhóm Y ──────────────────────────────────────────────────────────
        ('Ý', 'Y'), ('ý', 'y'), ('Ỳ', 'Y'), ('ỳ', 'y'),
        ('Ỷ', 'Y'), ('ỷ', 'y'), ('Ỹ', 'Y'), ('ỹ', 'y'),
        ('Ỵ', 'Y'), ('ỵ', 'y'),
    ];

    /// <summary>
    /// Bỏ toàn bộ dấu tiếng Việt và chuyển thường.
    /// "Nguyễn Hảo" → "nguyen hao" | "ĐỒNG HẢO" → "dong hao"
    /// </summary>
    public static string RemoveDiacritics(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        // Bước 1: Thay thế tường minh toàn bộ ký tự tiếng Việt → ASCII
        // (Đ/đ BẮT BUỘC; các ký tự còn lại là defense-in-depth)
        var sb = new StringBuilder(text);
        foreach (var (from, to) in VietnameseMap)
            sb.Replace(from, to);

        // Bước 2: NFD + strip combining marks (xử lý ký tự Latin diacritics còn sót)
        var nfd = sb.ToString().Normalize(NormalizationForm.FormD);
        var result = new StringBuilder(nfd.Length);
        foreach (var c in nfd)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                result.Append(c);
        }

        return result.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }
}
