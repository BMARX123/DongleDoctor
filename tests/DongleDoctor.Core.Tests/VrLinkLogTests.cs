using DongleDoctor.Core.Scan;

namespace DongleDoctor.Core.Tests;

public class VrLinkLogTests
{
    [Fact]
    public void Throttle_IsInMicroseconds()
    {
        var line = VrLinkLog.Parse("Sat Nov 14 2026 12:28:59.010 [Info] - THROTTLE EVENT: 300613");
        Assert.NotNull(line);
        Assert.Equal(ScanEventKind.Throttle, line.Kind);
        Assert.Equal(300.613, line.Ms, 3);
        Assert.Equal(new DateTime(2026, 11, 14, 12, 28, 59, 10), line.Local);
    }

    [Theory]
    [InlineData("Sat Nov 14 2026 12:26:56.672 [Info] - THROTTLE EVENT: -2067381555")] // Seen in a real log
    [InlineData("Sat Nov 14 2026 12:26:56.672 [Info] - THROTTLE EVENT: 0")]
    [InlineData("Sat Nov 14 2026 12:26:56.672 [Info] - THROTTLE EVENT: 99999999999")]
    [InlineData("Sat Nov 14 2026 12:26:56.672 [Info] - THROTTLE EVENT: soon")]
    public void Throttle_JunkValues_AreSkipped(string text) => Assert.Null(VrLinkLog.Parse(text));

    [Fact]
    public void BandwidthHistogram_MarksStreaming() =>
        Assert.Equal(ScanEventKind.Streaming,
            VrLinkLog.Parse("Sat Nov 14 2026 12:29:10.000 [Info] - Link BW hist: w 1 0 0 3 12 900")?.Kind);

    [Fact]
    public void SingleDigitDay_Parses() =>
        Assert.Equal(4, VrLinkLog.Parse("Wed Nov 4 2026 09:00:00.000 [Info] - THROTTLE EVENT: 150000")?.Local.Day);

    [Theory]
    [InlineData("Sat Nov 14 2026 12:29:10.000 [Info] - Bad link event: d=11.2 ms 350.0 mbit/s -> 300.0 mbit/s, ping 3.1 ms")]
    [InlineData("THROTTLE EVENT: 300613")]
    [InlineData("")]
    [InlineData("Sat Nov 99 2026 12:29:10.000 [Info] - THROTTLE EVENT: 300613")]
    public void OtherLines_AreSkipped(string text) => Assert.Null(VrLinkLog.Parse(text));
}
