using System.Globalization;
using System.Text.RegularExpressions;

namespace DongleDoctor.Core.Scan;

/// <summary>A line from Steam's VR Link driver log that the scan uses.</summary>
/// <param name="Local">The line's timestamp, in the PC's local time.</param>
public sealed record VrLinkLine(DateTime Local, ScanEventKind Kind, double Ms = 0);

/// <summary>
/// Parses Steam's driver_vrlink.txt. The format is undocumented, so anything unexpected is skipped rather than
/// guessed at. A line looks like: <c>Sat Nov 14 2026 12:28:59.010 [Info] - THROTTLE EVENT: 300613</c> (µs).
/// </summary>
public static partial class VrLinkLog
{
    // A throttle longer than this is junk, such as the negative or huge values Steam sometimes logs.
    private const double MaxThrottleMs = 60_000;

    public static VrLinkLine? Parse(string line)
    {
        var m = LineRegex().Match(line);
        if (!m.Success || !DateTime.TryParseExact(m.Groups["time"].Value, "MMM d yyyy HH:mm:ss.fff",
                CultureInfo.InvariantCulture, DateTimeStyles.AllowInnerWhite, out var local))
        {
            return null;
        }

        var message = m.Groups["msg"].Value;
        if (ThrottleRegex().Match(message) is { Success: true } t
            && double.TryParse(t.Groups["us"].Value, CultureInfo.InvariantCulture, out var us)
            && us / 1000 is > 0 and <= MaxThrottleMs)
        {
            return new VrLinkLine(local, ScanEventKind.Throttle, us / 1000);
        }

        return message.StartsWith("Link BW hist:", StringComparison.Ordinal)
            ? new VrLinkLine(local, ScanEventKind.Streaming)
            : null;
    }

    [GeneratedRegex(@"^\w{3} (?<time>\w{3} +\d+ \d{4} [\d:.]+) \[\w+\] - (?<msg>.*)$")]
    private static partial Regex LineRegex();

    [GeneratedRegex(@"^THROTTLE EVENT: (?<us>-?\d+)\s*$")]
    private static partial Regex ThrottleRegex();
}
