using DongleDoctor.Core.Checks;
using DongleDoctor.Core.Diagnostics;
using DongleDoctor.Core.Scan;
using DongleDoctor.Core.Wifi;

namespace DongleDoctor.Core.Tests;

public sealed class RunLogTests : IDisposable
{
    private static readonly DateTime Start = new(2026, 11, 15, 13, 40, 0);

    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"dd-logs-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private static ScanReport Report() => ScanAnalysis.Analyze(
        [
            new(TimeSpan.Zero, ScanEventKind.LogOpened),
            new(TimeSpan.FromSeconds(30), ScanEventKind.Streaming),
            new(TimeSpan.FromSeconds(20), ScanEventKind.Throttle, 300),
        ],
        TimeSpan.FromSeconds(45));

    [Fact]
    public void Scan_ListsHiccupsAtClockTime_AndTheChime()
    {
        var text = RunLog.FormatScan(Start, TimeSpan.FromMinutes(5), stoppedEarly: true, Report(), chimePlayed: null);

        Assert.Contains("ended 13:40:45", text);
        Assert.Contains("0:45 of 5:00, stopped early", text);
        Assert.Contains("13:40:19.700  peak 300 ms", text);
        Assert.Contains("Chime:       not due", text);
    }

    [Fact]
    public void ChecksStartAFile_ScansAppendToIt_AndOldFilesArePruned()
    {
        var log = new RunLog(_dir, keep: 2);
        log.Checks([new Finding("usb.speed", Severity.Pass, "USB 3", "")], Start);
        log.Scan(Start, TimeSpan.FromMinutes(5), false, Report(), chimePlayed: true);
        log.Checks([], Start.AddMinutes(1));
        log.Checks([], Start.AddMinutes(2));

        var files = Directory.GetFiles(_dir).Select(Path.GetFileName).Order().ToList();
        Assert.Equal(["run-20261115-134100.txt", "run-20261115-134200.txt"], files);
    }

    [Fact]
    public void Wifi_LeavesOutNetworkNames()
    {
        byte[] frameBssid = [2, 1, 1, 1, 1, 1];
        byte[] vht80 = [192, 5, 1, 42, 0, 0, 0];
        var survey = WifiAnalysis.Analyze(new WifiSweep(
            new WifiLink(frameBssid, 94, 1201, 1201),
            [
                new BssEntry("", frameBssid, 5180, -46, vht80),
                new BssEntry("Neighbour Net", [0, 5, 5, 5, 5, 5], 5220, -55, vht80),
            ]));

        var text = RunLog.FormatWifi(survey, WifiAnalysis.ToFinding(survey));

        Assert.Contains("Frame:   5 GHz ch 36–48, 80 MHz, -46 dBm", text);
        Assert.Contains("Overlap: ch 44/80 MHz, -55 dBm", text);
        Assert.DoesNotContain("Neighbour", text);
    }

    [Fact]
    public void Scan_BeforeAnyChecks_IsIgnored()
    {
        new RunLog(_dir).Scan(Start, TimeSpan.FromMinutes(5), false, Report(), true);
        Assert.False(Directory.Exists(_dir));
    }
}
