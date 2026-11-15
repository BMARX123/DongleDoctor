using DongleDoctor.Core.Checks;

namespace DongleDoctor.Core.Scan;

public enum HiccupSource
{
    /// <summary>Steam's encoder stopped: the stream itself paused.</summary>
    SteamThrottle,

    /// <summary>A ping to the Frame took too long; a proxy for the stream.</summary>
    Ping,
}

/// <summary>One pause, built from events less than <see cref="ScanAnalysis.MergeGap"/> apart.</summary>
/// <param name="PeakMs">The largest single throttle or ping in the episode.</param>
/// <param name="TotalMs">For Steam, the sum of the throttles, which is about how long the stream stalled.</param>
public sealed record HiccupEpisode(TimeSpan Start, TimeSpan End, HiccupSource Source, double PeakMs, double TotalMs);

/// <summary>What a scan found.</summary>
/// <param name="Hiccups">The episodes that count. Steam throttles when the Steam log was readable, otherwise pings.</param>
/// <param name="PingOnlySpikes">With the Steam log readable: ping spikes with no throttle nearby. Shown, not counted.</param>
/// <param name="UsedSteamLog">False means the count comes from pings alone and is less precise.</param>
/// <param name="Elapsed">Wall time the scan ran, including time not streaming.</param>
public sealed record ScanReport(
    ScanSummary Summary,
    IReadOnlyList<HiccupEpisode> Hiccups,
    IReadOnlyList<HiccupEpisode> PingOnlySpikes,
    bool UsedSteamLog,
    TimeSpan Elapsed,
    IReadOnlyList<string> ProbeErrors)
{
    /// <summary>False when the Frame never streamed during the scan, so there's nothing to judge.</summary>
    public bool StreamDetected => Summary.Duration > TimeSpan.Zero;
}

/// <summary>Turns a scan's events into hiccups and a summary for scoring.</summary>
public static class ScanAnalysis
{
    /// <summary>A throttle episode or a single ping at least this long is a hiccup. Provisional (one rig).</summary>
    public const double HiccupMs = 100;

    /// <summary>Events closer than this belong to the same episode.</summary>
    public static readonly TimeSpan MergeGap = TimeSpan.FromSeconds(1);

    /// <summary>Steam writes its bitrate histogram every 30 s while streaming.</summary>
    public static readonly TimeSpan StreamingMarkInterval = TimeSpan.FromSeconds(30);

    /// <summary>How long a ping waits for a reply. A ping that took this long was lost.</summary>
    public static readonly TimeSpan PingTimeout = TimeSpan.FromMilliseconds(500);

    public static ScanReport Analyze(IReadOnlyList<ScanEvent> events, TimeSpan elapsed, IReadOnlyList<string>? probeErrors = null)
    {
        var sorted = events.OrderBy(e => e.At).ToList();
        var throttles = sorted.Where(e => e.Kind == ScanEventKind.Throttle).ToList();
        var marks = sorted.Where(e => e.Kind == ScanEventKind.Streaming).Select(e => e.At).ToList();
        var usedSteamLog = throttles.Count > 0 || marks.Count > 0 || sorted.Any(e => e.Kind == ScanEventKind.LogOpened);

        // A throttle is logged when the encoder has already been stopped that long, so the stall began earlier.
        var steamEpisodes = Merge(throttles, HiccupSource.SteamThrottle, e => e.At - TimeSpan.FromMilliseconds(e.Ms))
            .Where(ep => ep.PeakMs >= HiccupMs)
            .ToList();
        var pingEpisodes = Merge(
            sorted.Where(e => e.Kind == ScanEventKind.Ping && e.Ms >= HiccupMs).ToList(), HiccupSource.Ping, e => e.At);

        var hiccups = usedSteamLog ? steamEpisodes : pingEpisodes;
        var pingOnly = usedSteamLog ? pingEpisodes.Where(p => !steamEpisodes.Any(s => Near(p, s))).ToList() : [];
        var streaming = (usedSteamLog, marks.Count > 0, throttles.Count > 0) switch
        {
            (true, true, _) => StreamingTime(marks, elapsed),

            // Throttles only happen while streaming, even if the scan was too short for a mark.
            (true, false, true) => elapsed,

            // A readable log with nothing in it: the Frame wasn't streaming.
            (true, false, false) => TimeSpan.Zero,

            // No Steam log: the best sign of a stream is the Frame answering pings.
            _ => sorted.Any(e => e.Kind == ScanEventKind.Ping && e.Ms < PingTimeout.TotalMilliseconds) ? elapsed : TimeSpan.Zero,
        };
        return new ScanReport(
            new ScanSummary(hiccups.Count, streaming, HasPcCause: null),
            hiccups, pingOnly, usedSteamLog, elapsed, probeErrors ?? []);
    }

    private static List<HiccupEpisode> Merge(List<ScanEvent> events, HiccupSource source, Func<ScanEvent, TimeSpan> startOf)
    {
        var episodes = new List<HiccupEpisode>();
        foreach (var e in events)
        {
            var last = episodes.Count > 0 ? episodes[^1] : null;
            if (last is not null && e.At - last.End < MergeGap)
            {
                episodes[^1] = last with { End = e.At, PeakMs = Math.Max(last.PeakMs, e.Ms), TotalMs = last.TotalMs + e.Ms };
            }
            else
            {
                episodes.Add(new HiccupEpisode(startOf(e), e.At, source, e.Ms, e.Ms));
            }
        }

        return episodes;
    }

    private static bool Near(HiccupEpisode a, HiccupEpisode b) =>
        a.Start - MergeGap <= b.End && b.Start - MergeGap <= a.End;

    /// <summary>
    /// Each streaming mark covers the 30 s before it, and the last one is assumed to carry on for up to 30 s,
    /// since the scan may end before Steam writes the next one.
    /// </summary>
    internal static TimeSpan StreamingTime(IReadOnlyList<TimeSpan> marks, TimeSpan elapsed)
    {
        var intervals = marks.Select(m => (Start: m - StreamingMarkInterval, End: m))
            .Append((Start: marks[^1], End: marks[^1] + StreamingMarkInterval))
            .Select(i => (Start: Clamp(i.Start, elapsed), End: Clamp(i.End, elapsed)))
            .OrderBy(i => i.Start);

        var total = TimeSpan.Zero;
        var coveredTo = TimeSpan.Zero;
        foreach (var (start, end) in intervals)
        {
            var from = start > coveredTo ? start : coveredTo;
            if (end > from)
            {
                total += end - from;
                coveredTo = end;
            }
        }

        return total;
    }

    private static TimeSpan Clamp(TimeSpan t, TimeSpan elapsed) => t < TimeSpan.Zero ? TimeSpan.Zero : t > elapsed ? elapsed : t;
}
