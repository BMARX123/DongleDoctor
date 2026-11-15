namespace DongleDoctor.Core.Scan;

public enum ScanEventKind
{
    /// <summary>Steam's encoder had been stopped for <see cref="ScanEvent.Ms"/> when it logged a throttle event.</summary>
    Throttle,

    /// <summary>A Steam log line that's only written while streaming (the bitrate histogram, every 30 s).</summary>
    Streaming,

    /// <summary>One ping to the Frame; <see cref="ScanEvent.Ms"/> is the round trip, or the timeout if no reply came.</summary>
    Ping,
}

/// <summary>Something a probe saw, timed from the start of the scan.</summary>
public sealed record ScanEvent(TimeSpan At, ScanEventKind Kind, double Ms = 0);

/// <summary>The scan's time origin. Probes use it to place both monotonic and wall-clock times on one timeline.</summary>
public sealed class ScanClock
{
    private readonly long _startTicks;

    public ScanClock(TimeProvider? time = null)
    {
        Time = time ?? TimeProvider.System;
        _startTicks = Time.GetTimestamp();
        StartLocal = Time.GetLocalNow().DateTime;
    }

    public TimeProvider Time { get; }

    /// <summary>Wall-clock local time at the start of the scan.</summary>
    public DateTime StartLocal { get; }

    public TimeSpan Now => Time.GetElapsedTime(_startTicks);

    /// <summary>Places a local wall-clock time (such as a Steam log timestamp) on the scan's timeline.</summary>
    public TimeSpan FromLocal(DateTime local) => local - StartLocal;
}

/// <summary>A continuous sampler that runs for the length of a scan.</summary>
public interface IProbe
{
    string Name { get; }

    /// <summary>Samples until <paramref name="cancellationToken"/> fires, passing each event to <paramref name="emit"/>.</summary>
    Task RunAsync(ScanClock clock, Action<ScanEvent> emit, CancellationToken cancellationToken);
}
