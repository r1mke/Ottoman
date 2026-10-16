using System.Globalization;
using System.Text;

namespace Ottoman.Domain.Recommendations;

/// <summary>
/// Turns free text (Bosnian/Croatian/Serbian or English) into comparable tokens: lower-case, no diacritics
/// (č/ć/š/ž/đ match c/c/s/z/d typed on any keyboard), no stop words.
/// </summary>
public static class TextNormalizer
{
    private static readonly HashSet<string> StopWords =
    [
        "i", "a", "u", "na", "za", "sa", "s", "od", "do", "je", "su", "se", "to", "ili", "da", "ne", "uz", "po", "pri",
        "kao", "koji", "koja", "koje", "ali", "iz", "o", "te", "ovo", "taj", "sve", "kod", "bez",
        "the", "and", "of", "for", "with", "to", "in", "a", "an", "is",
    ];

    public static string StripDiacritics(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(c switch { 'đ' => 'd', 'Đ' => 'D', _ => c });
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    public static IReadOnlyList<string> Tokens(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];

        var clean = StripDiacritics(text).ToLowerInvariant();
        var tokens = new List<string>();
        var current = new StringBuilder();
        void Flush()
        {
            if (current.Length >= 2 && !StopWords.Contains(current.ToString())) tokens.Add(current.ToString());
            current.Clear();
        }

        foreach (var c in clean)
        {
            if (char.IsLetterOrDigit(c)) current.Append(c);
            else Flush();
        }
        Flush();
        return tokens;
    }
}
