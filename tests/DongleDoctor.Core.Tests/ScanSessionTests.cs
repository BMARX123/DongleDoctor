using DongleDoctor.Core.Scan;

namespace DongleDoctor.Core.Tests;

public class ScanSessionTests
{
    [Fact]
    public async Task RunsForTheDuration_AndAnalyzesEvents()
    {
        var probe = new FakeProbe(new ScanEvent(TimeSpan.FromMilliseconds(10), ScanEventKind.Throttle, 300));
        var report = await new ScanSession([probe]).RunAsync(TimeSpan.FromMilliseconds(300));

        Assert.Equal(1, report.Summary.Hiccups);
        Assert.True(report.Elapsed >= TimeSpan.FromMilliseconds(300));
        Assert.True(probe.WasCancelled);
    }

    [Fact]
    public async Task StopEarly_ReturnsTheScanSoFar()
    {
        using var stop = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var report = await new ScanSession([new FakeProbe()]).RunAsync(TimeSpan.FromMinutes(5), stop: stop.Token);
        Assert.True(report.Elapsed < TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task BrokenProbe_IsReported_OthersCarryOn()
    {
        var good = new FakeProbe(new ScanEvent(TimeSpan.Zero, ScanEventKind.Ping, 150));
        var report = await new ScanSession([new BrokenProbe(), good]).RunAsync(TimeSpan.FromMilliseconds(200));

        Assert.Contains(report.ProbeErrors, e => e.StartsWith("Broken: "));
        Assert.Equal(1, report.Summary.Hiccups);
    }

    [Fact]
    public async Task ReportsProgress()
    {
        var updates = new List<ScanProgress>();
        await new ScanSession([new FakeProbe()]).RunAsync(TimeSpan.FromMilliseconds(1200), new SyncProgress(updates.Add));
        Assert.True(updates.Count >= 2);
        Assert.All(updates, u => Assert.Equal(TimeSpan.FromMilliseconds(1200), u.Duration));
    }

    private sealed class FakeProbe(params ScanEvent[] events) : IProbe
    {
        public bool WasCancelled { get; private set; }

        public string Name => "Fake";

        public async Task RunAsync(ScanClock clock, Action<ScanEvent> emit, CancellationToken cancellationToken)
        {
            foreach (var e in events)
            {
                emit(e);
            }

            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            finally
            {
                WasCancelled = cancellationToken.IsCancellationRequested;
            }
        }
    }

    private sealed class BrokenProbe : IProbe
    {
        public string Name => "Broken";

        public Task RunAsync(ScanClock clock, Action<ScanEvent> emit, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("no log");
    }

    // Progress<T> posts to the thread pool; this reports inline so the test sees every update.
    private sealed class SyncProgress(Action<ScanProgress> report) : IProgress<ScanProgress>
    {
        public void Report(ScanProgress value) => report(value);
    }
}
