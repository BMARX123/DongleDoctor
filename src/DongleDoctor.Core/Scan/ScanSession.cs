namespace DongleDoctor.Core.Scan;

/// <summary>Live progress for the Scanning screen.</summary>
public sealed record ScanProgress(TimeSpan Elapsed, TimeSpan Duration, int HiccupsSoFar);

/// <summary>Runs the probes for a fixed time, then analyzes what they saw. Use a new session for each scan.</summary>
public sealed class ScanSession(IReadOnlyList<IProbe> probes, TimeProvider? time = null)
{
    private static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(500);

    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private readonly List<ScanEvent> _events = [];
    private readonly List<string> _errors = [];

    /// <summary>
    /// Scans for <paramref name="duration"/>. Cancelling <paramref name="stop"/> ends the scan early and still
    /// returns a report covering the time so far.
    /// </summary>
    public async Task<ScanReport> RunAsync(TimeSpan duration, IProgress<ScanProgress>? progress = null, CancellationToken stop = default)
    {
        var clock = new ScanClock(_time);
        using var end = CancellationTokenSource.CreateLinkedTokenSource(stop);
        end.CancelAfter(duration);

        var running = probes.Select(p => RunProbeAsync(p, clock, end.Token)).ToList();
        using (var timer = new PeriodicTimer(ProgressInterval, _time))
        {
            try
            {
                while (await timer.WaitForNextTickAsync(end.Token).ConfigureAwait(false))
                {
                    progress?.Report(new ScanProgress(clock.Now, duration, Analyze(clock.Now).Hiccups.Count));
                }
            }
            catch (OperationCanceledException)
            {
                // The scan ran its course or the user stopped it.
            }
        }

        await Task.WhenAll(running).ConfigureAwait(false);
        var report = Analyze(clock.Now);
        progress?.Report(new ScanProgress(report.Elapsed, duration, report.Hiccups.Count));
        return report;
    }

    private ScanReport Analyze(TimeSpan elapsed)
    {
        lock (_events)
        {
            return ScanAnalysis.Analyze([.. _events], elapsed, [.. _errors]);
        }
    }

    private async Task RunProbeAsync(IProbe probe, ScanClock clock, CancellationToken cancellationToken)
    {
        try
        {
            await probe.RunAsync(clock, e =>
            {
                lock (_events)
                {
                    _events.Add(e);
                }
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception e)
        {
            // One broken probe never stops the scan; the others carry on.
            lock (_events)
            {
                _errors.Add($"{probe.Name}: {e.Message}");
            }
        }
    }
}
