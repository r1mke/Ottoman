using System.Globalization;
using System.Text.RegularExpressions;

namespace Ottoman.Application.Salon;

/// <summary>Pulls coordinates out of a pasted Google Maps link so the admin does not have to type them.</summary>
public static partial class MapsLink
{
    // Forms Google Maps uses: .../@43.85,18.43,17z   ...!3d43.85!4d18.43   ?query=43.85,18.43   ?q=/ll=/destination=43.85,18.43
    [GeneratedRegex(@"@(-?\d{1,2}(?:\.\d+)?),(-?\d{1,3}(?:\.\d+)?)")]
    private static partial Regex AtForm();

    [GeneratedRegex(@"!3d(-?\d{1,2}(?:\.\d+)?)!4d(-?\d{1,3}(?:\.\d+)?)")]
    private static partial Regex DataForm();

    [GeneratedRegex(@"[?&](?:query|q|ll|destination|center)=(-?\d{1,2}(?:\.\d+)?)(?:,|%2C)\s*(-?\d{1,3}(?:\.\d+)?)", RegexOptions.IgnoreCase)]
    private static partial Regex QueryForm();

    public static bool TryExtractCoordinates(string? url, out double latitude, out double longitude)
    {
        latitude = longitude = 0;
        if (string.IsNullOrWhiteSpace(url)) return false;
        // The pin ("!3d..!4d..") is the place itself; "@" is only where the map happened to be centred.
        foreach (var re in new[] { DataForm(), QueryForm(), AtForm() })
        {
            var m = re.Match(url);
            if (!m.Success) continue;
            if (double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var lat)
                && double.TryParse(m.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var lng)
                && lat is >= -90 and <= 90 && lng is >= -180 and <= 180)
            {
                latitude = lat;
                longitude = lng;
                return true;
            }
        }
        return false;
    }
}
