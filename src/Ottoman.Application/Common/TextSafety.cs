namespace Ottoman.Application.Common;

/// <summary>
/// Characters no name or free text may contain: NUL and the other control characters (tab and line breaks are fine),
/// and the Unicode bidi overrides/isolates (U+202A-U+202E, U+2066-U+2069) that can make "gpj.exe" display as "exe.jpg"
/// or reorder a name in a list. Checked once for every request, before validation.
/// </summary>
public static class TextSafety
{
    public const string Message = "Tekst sadrži nedozvoljene znakove.";

    public static bool IsForbidden(char c) =>
        (c < ' ' && c is not ('\t' or '\n' or '\r'))
        || c == '\u007F'
        || c is >= '\u202A' and <= '\u202E'
        || c is >= '\u2066' and <= '\u2069';

    public static bool IsSafe(string? text)
    {
        if (string.IsNullOrEmpty(text)) return true;
        foreach (var c in text)
            if (IsForbidden(c)) return false;
        return true;
    }
}
