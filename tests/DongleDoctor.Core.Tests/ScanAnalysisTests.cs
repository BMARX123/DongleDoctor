using DongleDoctor.Core.Scan;

namespace DongleDoctor.Core.Tests;

public class ScanAnalysisTests
{
    private static readonly TimeSpan FiveMinutes = TimeSpan.FromMinutes(5);

    private static ScanEvent Throttle(double seconds, double ms) => new(TimeSpan.FromSeconds(seconds), ScanEventKind.Throttle, ms);

    private static ScanEvent Ping(double seconds, double ms) => new(TimeSpan.FromSeconds(seconds), ScanEventKind.Ping, ms);

    private static ScanEvent Mark(double seconds) => new(TimeSpan.FromSeconds(seconds), ScanEventKind.Streaming);

    /// <summary>Streaming marks every 30 s for the whole scan.</summary>
    private static IEnumerable<ScanEvent> Streaming(TimeSpan length) =>
        Enumerable.Range(1, (int)(length.TotalSeconds / 30)).Select(i => Mark(i * 30));

    [Fact]
    public void ThrottlesUnderOneSecondApart_AreOneEpisode()
    {
        var r = ScanAnalysis.Analyze([.. Streaming(FiveMinutes), Throttle(10, 300), Throttle(10.25, 120), Throttle(10.4, 80)], FiveMinutes);
        var episode = Assert.Single(r.Hiccups);
        Assert.Equal(300, episode.PeakMs);
        Assert.Equal(500, episode.TotalMs);
        Assert.Equal(TimeSpan.FromSeconds(9.7), episode.Start);
    }

    [Fact]
    public void ShortThrottles_AreNotHiccups() =>
        Assert.Empty(ScanAnalysis.Analyze([.. Streaming(FiveMinutes), Throttle(10, 55), Throttle(40, 99)], FiveMinutes).Hiccups);

    [Fact]
    public void WithSteamLog_CountsThrottlesOnly_AndListsPingOnlySpikes()
    {
        var r = ScanAnalysis.Analyze(
            [.. Streaming(FiveMinutes), Throttle(10, 300), Ping(9.8, 280), Ping(100, 150), Ping(101.5, 2)],
            FiveMinutes);

        Assert.True(r.UsedSteamLog);
        Assert.Equal(1, r.Summary.Hiccups);
        var spike = Assert.Single(r.PingOnlySpikes);
        Assert.Equal(TimeSpan.FromSeconds(100), spike.Start);
    }

    [Fact]
    public void WithoutSteamLog_CountsPingEpisodes()
    {
        var r = ScanAnalysis.Analyze([Ping(5, 2), Ping(10, 250), Ping(10.5, 500), Ping(60, 120)], FiveMinutes);
        Assert.False(r.UsedSteamLog);
        Assert.Equal(2, r.Summary.Hiccups);
        Assert.Empty(r.PingOnlySpikes);
        Assert.Equal(FiveMinutes, r.Summary.Duration);
    }

    [Fact]
    public void CauseIsUnknown_UntilCorrelationExists() =>
        Assert.Null(ScanAnalysis.Analyze([Throttle(10, 300)], FiveMinutes).Summary.HasPcCause);

    [Fact]
    public void StreamingTime_CountsOnlyStretchesWithMarks()
    {
        // Streamed from 0 to 90 s, paused, then streamed again from 210 s to the end.
        var marks = new[] { 30, 60, 90, 240, 270, 300 }.Select(s => TimeSpan.FromSeconds(s)).ToList();
        Assert.Equal(TimeSpan.FromSeconds(180), ScanAnalysis.StreamingTime(marks, FiveMinutes));
    }

    [Fact]
    public void StreamingTime_LastMarkCarriesOnUntilTheEnd()
    {
        // The scan ended 20 s after the last mark, before Steam wrote the next one.
        var marks = new[] { 30, 60 }.Select(s => TimeSpan.FromSeconds(s)).ToList();
        Assert.Equal(TimeSpan.FromSeconds(80), ScanAnalysis.StreamingTime(marks, TimeSpan.FromSeconds(80)));
    }

    [Fact]
    public void ThrottlesWithoutMarks_UseTheWholeScan() =>
        Assert.Equal(FiveMinutes, ScanAnalysis.Analyze([Throttle(10, 300)], FiveMinutes).Summary.Duration);
}
