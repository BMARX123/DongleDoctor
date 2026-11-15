using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DongleDoctor.Core.Scan;

namespace DongleDoctor.App.ViewModels;

/// <summary>The Scanning screen: progress, hiccups so far on a timeline, and Stop early.</summary>
public partial class ScanningViewModel(ScanSession session, TimeSpan duration, Action<ScanReport, bool> finished) : ViewModelBase
{
    /// <summary>How long to wait for a stream before asking the user to start a game.</summary>
    public static readonly TimeSpan NoStreamWarningAfter = TimeSpan.FromSeconds(60);

    private readonly CancellationTokenSource _stop = new();

    [ObservableProperty]
    public partial double Progress { get; set; }

    [ObservableProperty]
    public partial string TimeLeft { get; set; } = Format(duration);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HiccupsLabel))]
    public partial int Hiccups { get; set; }

    public string HiccupsLabel => Hiccups == 1 ? "hiccup so far" : "hiccups so far";

    /// <summary>Where each hiccup falls on the timeline, from 0 (the start) to 1 (the end).</summary>
    [ObservableProperty]
    public partial IReadOnlyList<double> HiccupMarks { get; set; } = [];

    [ObservableProperty]
    public partial bool ShowNoStreamWarning { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StopEarlyCommand))]
    public partial bool IsStopping { get; set; }

    public async Task RunAsync()
    {
        var report = await session.RunAsync(duration, new Progress<ScanProgress>(Update), _stop.Token);
        finished(report, _stop.IsCancellationRequested);
        _stop.Dispose();
    }

    internal void Update(ScanProgress p)
    {
        Progress = Math.Clamp(p.Elapsed / p.Duration, 0, 1);
        TimeLeft = Format(p.Duration - p.Elapsed);
        Hiccups = p.HiccupsSoFar;
        HiccupMarks = [.. p.HiccupTimes.Select(t => Math.Clamp(t / p.Duration, 0, 1))];
        ShowNoStreamWarning = !p.StreamDetected && p.Elapsed >= NoStreamWarningAfter;
    }

    private bool CanStopEarly() => !IsStopping;

    [RelayCommand(CanExecute = nameof(CanStopEarly))]
    private void StopEarly()
    {
        IsStopping = true;
        _stop.Cancel();
    }

    private static string Format(TimeSpan left) =>
        left <= TimeSpan.Zero ? "Finishing…" : $"{(int)left.TotalMinutes}:{left.Seconds:00} left";
}
