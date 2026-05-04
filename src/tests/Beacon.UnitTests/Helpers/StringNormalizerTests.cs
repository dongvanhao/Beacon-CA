using Beacon.Shared.Helpers;
using FluentAssertions;

namespace Beacon.UnitTests.Helpers;

public class StringNormalizerTests
{
    // ── Nhóm Đ (KHÔNG phân giải qua NFD — BẮT BUỘC explicit) ────────────────
    [Theory]
    [InlineData("Đồng", "dong")]
    [InlineData("đồng", "dong")]
    [InlineData("ĐỒNG", "dong")]
    [InlineData("ĐỒNGhao", "donghao")]
    public void RemoveDiacritics_D_Group(string input, string expected)
        => StringNormalizer.RemoveDiacritics(input).Should().Be(expected);

    // ── Nhóm A ───────────────────────────────────────────────────────────────
    [Theory]
    [InlineData("ắ", "a")] [InlineData("Ắ", "a")]
    [InlineData("ặ", "a")] [InlineData("Ặ", "a")]
    [InlineData("ằ", "a")] [InlineData("Ằ", "a")]
    [InlineData("ẳ", "a")] [InlineData("Ẳ", "a")]
    [InlineData("ẵ", "a")] [InlineData("Ẵ", "a")]
    [InlineData("ă", "a")] [InlineData("Ă", "a")]
    [InlineData("ấ", "a")] [InlineData("Ấ", "a")]
    [InlineData("ậ", "a")] [InlineData("Ậ", "a")]
    [InlineData("ầ", "a")] [InlineData("Ầ", "a")]
    [InlineData("ẩ", "a")] [InlineData("Ẩ", "a")]
    [InlineData("ẫ", "a")] [InlineData("Ẫ", "a")]
    [InlineData("â", "a")] [InlineData("Â", "a")]
    [InlineData("á", "a")] [InlineData("Á", "a")]
    [InlineData("à", "a")] [InlineData("À", "a")]
    [InlineData("ả", "a")] [InlineData("Ả", "a")]
    [InlineData("ã", "a")] [InlineData("Ã", "a")]
    [InlineData("ạ", "a")] [InlineData("Ạ", "a")]
    public void RemoveDiacritics_A_Group(string input, string expected)
        => StringNormalizer.RemoveDiacritics(input).Should().Be(expected);

    // ── Nhóm E ───────────────────────────────────────────────────────────────
    [Theory]
    [InlineData("ế", "e")] [InlineData("Ế", "e")]
    [InlineData("ệ", "e")] [InlineData("Ệ", "e")]
    [InlineData("ề", "e")] [InlineData("Ề", "e")]
    [InlineData("ể", "e")] [InlineData("Ể", "e")]
    [InlineData("ễ", "e")] [InlineData("Ễ", "e")]
    [InlineData("ê", "e")] [InlineData("Ê", "e")]
    [InlineData("é", "e")] [InlineData("É", "e")]
    [InlineData("è", "e")] [InlineData("È", "e")]
    [InlineData("ẻ", "e")] [InlineData("Ẻ", "e")]
    [InlineData("ẽ", "e")] [InlineData("Ẽ", "e")]
    [InlineData("ẹ", "e")] [InlineData("Ẹ", "e")]
    public void RemoveDiacritics_E_Group(string input, string expected)
        => StringNormalizer.RemoveDiacritics(input).Should().Be(expected);

    // ── Nhóm I ───────────────────────────────────────────────────────────────
    [Theory]
    [InlineData("í", "i")] [InlineData("Í", "i")]
    [InlineData("ì", "i")] [InlineData("Ì", "i")]
    [InlineData("ỉ", "i")] [InlineData("Ỉ", "i")]
    [InlineData("ĩ", "i")] [InlineData("Ĩ", "i")]
    [InlineData("ị", "i")] [InlineData("Ị", "i")]
    public void RemoveDiacritics_I_Group(string input, string expected)
        => StringNormalizer.RemoveDiacritics(input).Should().Be(expected);

    // ── Nhóm O ───────────────────────────────────────────────────────────────
    [Theory]
    [InlineData("ố", "o")] [InlineData("Ố", "o")]
    [InlineData("ộ", "o")] [InlineData("Ộ", "o")]
    [InlineData("ồ", "o")] [InlineData("Ồ", "o")]
    [InlineData("ổ", "o")] [InlineData("Ổ", "o")]
    [InlineData("ỗ", "o")] [InlineData("Ỗ", "o")]
    [InlineData("ô", "o")] [InlineData("Ô", "o")]
    [InlineData("ớ", "o")] [InlineData("Ớ", "o")]
    [InlineData("ợ", "o")] [InlineData("Ợ", "o")]
    [InlineData("ờ", "o")] [InlineData("Ờ", "o")]
    [InlineData("ở", "o")] [InlineData("Ở", "o")]
    [InlineData("ỡ", "o")] [InlineData("Ỡ", "o")]
    [InlineData("ơ", "o")] [InlineData("Ơ", "o")]
    [InlineData("ó", "o")] [InlineData("Ó", "o")]
    [InlineData("ò", "o")] [InlineData("Ò", "o")]
    [InlineData("ỏ", "o")] [InlineData("Ỏ", "o")]
    [InlineData("õ", "o")] [InlineData("Õ", "o")]
    [InlineData("ọ", "o")] [InlineData("Ọ", "o")]
    public void RemoveDiacritics_O_Group(string input, string expected)
        => StringNormalizer.RemoveDiacritics(input).Should().Be(expected);

    // ── Nhóm U ───────────────────────────────────────────────────────────────
    [Theory]
    [InlineData("ứ", "u")] [InlineData("Ứ", "u")]
    [InlineData("ự", "u")] [InlineData("Ự", "u")]
    [InlineData("ừ", "u")] [InlineData("Ừ", "u")]
    [InlineData("ử", "u")] [InlineData("Ử", "u")]
    [InlineData("ữ", "u")] [InlineData("Ữ", "u")]
    [InlineData("ư", "u")] [InlineData("Ư", "u")]
    [InlineData("ú", "u")] [InlineData("Ú", "u")]
    [InlineData("ù", "u")] [InlineData("Ù", "u")]
    [InlineData("ủ", "u")] [InlineData("Ủ", "u")]
    [InlineData("ũ", "u")] [InlineData("Ũ", "u")]
    [InlineData("ụ", "u")] [InlineData("Ụ", "u")]
    public void RemoveDiacritics_U_Group(string input, string expected)
        => StringNormalizer.RemoveDiacritics(input).Should().Be(expected);

    // ── Nhóm Y ───────────────────────────────────────────────────────────────
    [Theory]
    [InlineData("ý", "y")] [InlineData("Ý", "y")]
    [InlineData("ỳ", "y")] [InlineData("Ỳ", "y")]
    [InlineData("ỷ", "y")] [InlineData("Ỷ", "y")]
    [InlineData("ỹ", "y")] [InlineData("Ỹ", "y")]
    [InlineData("ỵ", "y")] [InlineData("Ỵ", "y")]
    public void RemoveDiacritics_Y_Group(string input, string expected)
        => StringNormalizer.RemoveDiacritics(input).Should().Be(expected);

    // ── Tên thực tế tiếng Việt ───────────────────────────────────────────────
    [Theory]
    [InlineData("Nguyễn Hảo",    "nguyen hao")]
    [InlineData("NGUYỄN HẢO",    "nguyen hao")]
    [InlineData("nguyễn hảo",    "nguyen hao")]
    [InlineData("Đồng Hảo",      "dong hao")]
    [InlineData("ĐỒNG HẢO",      "dong hao")]
    [InlineData("ĐỒNGHảo",       "donghao")]
    [InlineData("Trần Thị Hương", "tran thi huong")]
    [InlineData("Lê Văn Đức",     "le van duc")]
    [InlineData("Phạm Thị Bích",  "pham thi bich")]
    [InlineData("Vũ Thị Thúy",    "vu thi thuy")]
    [InlineData("Hoàng Anh Tú",   "hoang anh tu")]
    [InlineData("Đinh Thị Mỹ",    "dinh thi my")]
    public void RemoveDiacritics_RealVietnameseNames(string input, string expected)
        => StringNormalizer.RemoveDiacritics(input).Should().Be(expected);

    // ── Edge cases ───────────────────────────────────────────────────────────
    [Theory]
    [InlineData("",    "")]
    [InlineData("   ", "")]
    [InlineData("abc", "abc")]
    [InlineData("ABC", "abc")]
    [InlineData("Dong Hao", "dong hao")]    // no diacritics → pass-through
    public void RemoveDiacritics_EdgeCases(string input, string expected)
        => StringNormalizer.RemoveDiacritics(input).Should().Be(expected);
}
