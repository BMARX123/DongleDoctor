using System.Globalization;
using System.Net;
using DongleDoctor.Core.Scan;
using DongleDoctor.Platform.Windows.Scan;
using Xunit.Abstractions;

namespace DongleDoctor.Platform.Windows.Tests;

public sealed class ScanProbeTests(ITestOutputHelper output) : IDisposable
{
    private readonly string _log = Path.Combine(Path.GetTempPath(), $"dd-vrlink-{Guid.NewGuid():N}.txt");

    public void Dispose() => File.Delete(_log);

    private static string Line(string message) =>
        DateTime.Now.ToString("ddd MMM d yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture) + " [Info] - " + message + "\r\n";

    [Fact]
    public async Task SteamLog_ReadsOnlyLinesWrittenDuringTheScan()
    {
        File.WriteAllText(_log, Line("THROTTLE EVENT: 400000")); // Before the scan: ignored.
        var events = await RunWhile(new SteamLogProbe(_log), async () =>
        {
            File.AppendAllText(_log, Line("THROTTLE EVENT: 300613") + Line("Link BW hist: w 1 2 3"));
            File.AppendAllText(_log, "Sat Nov 14 2026 12:00:00.000 [Info] - THROTTLE EV"); // A partial line: not yet.
            await Task.Delay(600);
        });

        Assert.Equal([ScanEventKind.Throttle, ScanEventKind.Streaming], events.Select(e => e.Kind));
        Assert.Equal(300.613, events[0].Ms, 3);
    }

    [Fact]
    public async Task SteamLog_NewLogAfterRestart_IsReadFromTheTop()
    {
        File.WriteAllText(_log, "//===== old session header, long enough to tell the files apart\r\n" + Line("THROTTLE EVENT: 400000"));
        var events = await RunWhile(new SteamLogProbe(_log), async () =>
        {
            await Task.Delay(400);
            File.WriteAllText(_log, "//===== new session\r\n" + Line("THROTTLE EVENT: 250000"));
            await Task.Delay(600);
        });

        Assert.Equal(250, Assert.Single(events).Ms);
    }

    [Fact]
    public async Task SteamLog_MissingFile_WaitsForIt()
    {
        var events = await RunWhile(new SteamLogProbe(_log), async () =>
        {
            await Task.Delay(300);
            File.WriteAllText(_log, Line("THROTTLE EVENT: 150000"));
            await Task.Delay(600);
        });

        Assert.Single(events);
    }

    [Fact]
    public async Task Icmp_PingsTheTarget()
    {
        var events = await RunWhile(new IcmpProbe(() => IPAddress.Loopback), () => Task.Delay(500));
        output.WriteLine($"{events.Count} pings in 500 ms, max {events.Max(e => e.Ms):N1} ms");
        Assert.True(events.Count >= 5);
        Assert.All(events, e => Assert.Equal(ScanEventKind.Ping, e.Kind));
    }

    [Fact]
    public void FindFrameAddress_DoesNotThrow() =>
        output.WriteLine(IcmpProbe.FindFrameAddress()?.ToString() ?? "Dongle not connected to the Frame.");

    [Fact]
    public void SteamLogPath_IsUnderSteam() =>
        output.WriteLine(SteamLogProbe.DefaultPath() ?? "Steam isn't installed.");

    private static async Task<List<ScanEvent>> RunWhile(IProbe probe, Func<Task> during)
    {
        var events = new List<ScanEvent>();
        using var stop = new CancellationTokenSource();
        var run = probe.RunAsync(new ScanClock(), e => { lock (events) { events.Add(e); } }, stop.Token);
        await during();
        stop.Cancel();
        try
        {
            await run;
        }
        catch (OperationCanceledException)
        {
        }

        return events;
    }
}
