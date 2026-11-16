using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using DongleDoctor.Core.Checks;
using DongleDoctor.Core.Scan;

namespace DongleDoctor.App.ViewModels;

/// <summary>The results screen for all three verdicts: Ready, Not ready, and hiccups that don't come from the PC.</summary>
public partial class ResultViewModel : ViewModelBase
{
    private readonly Action _checkAgain;
    private readonly Action _startScan;
    private readonly Action _close;

    /// <param name="scanReport">The scan that ran, if any, including one that never saw a stream.</param>
    public ResultViewModel(Assessment assessment, ScanReport? scanReport, Action checkAgain, Action startScan, Action close, Action<string> openGuide)
    {
        _checkAgain = checkAgain;
        _startScan = startScan;
        _close = close;

        // A failing check comes first: scanning before it's fixed would only measure the problem we already know about.
        OfferScan = assessment.Verdict == Verdict.Ready && assessment.Scan is null;
        ScanButtonText = scanReport is null ? "Scan while you play" : "Scan again";
        HasTimeline = scanReport is { StreamDetected: true, Elapsed.Ticks: > 0 };
        HiccupMarks = HasTimeline
            ? [.. scanReport!.Hiccups.Select(h => Math.Clamp(h.Start / scanReport.Elapsed, 0, 1))]
            : [];
        TimelineLength = HasTimeline ? $"{(int)scanReport!.Elapsed.TotalMinutes}:{scanReport.Elapsed.Seconds:00}" : "";
        ScanNote = scanReport switch
        {
            { StreamDetected: false } => "The scan couldn't run: your Frame wasn't streaming. Start a game on your Frame, then scan again.",
            { UsedSteamLog: false } => "Steam's VR Link log couldn't be read, so hiccups were counted from pings to your Frame. This is less precise.",
            null when OfferScan => "Next, scan while you play to find any hiccups.",
            _ => null,
        };

        Verdict = assessment.Verdict;
        ChecksSummary = $"{assessment.ChecksPassed} of {assessment.ChecksRun}";
        HasScanTiles = assessment.Scan is not null;
        HiccupCount = assessment.Scan?.Hiccups ?? 0;
        // The second tile counts optional improvements; "9 of 11" would read as a failure on a Ready screen.
        (ChecksTileLabel, ChecksTileValue) = assessment.Improvements.Count > 0
            ? ("Optional improvements", $"{assessment.Improvements.Count}")
            : ("Checks", $"All {assessment.ChecksRun} passed");
        ToFix = [.. assessment.ToFix.Select(f => new FindingRowViewModel(f, openGuide))];
        Improvements = [.. assessment.Improvements.Select(f => new FindingRowViewModel(f, openGuide))];

        var scan = assessment.Scan;
        var hiccups = scan is null ? null : $"{Count(scan.Hiccups, "hiccup")} in {Count((int)Math.Round(scan.Duration.TotalMinutes), "minute")}.";
        (Headline, Summary) = Verdict switch
        {
            Verdict.NotReady => ("Not ready yet", $"{hiccups} {Count(ToFix.Count, "thing")} to fix.".TrimStart()),
            Verdict.HiccupsNotFromPc => ("Your PC checks all passed, but you had hiccups", $"{hiccups} They most likely come from the wireless link."),
            _ => scan is null
                ? ("Your setup looks good", "Your dongle is connected the way it should be.")
                : ("You're ready", $"{hiccups} Your connection stayed steady."),
        };
    }

    public Verdict Verdict { get; }

    public bool IsReady => Verdict == Verdict.Ready;

    public bool IsNotReady => Verdict == Verdict.NotReady;

    public bool IsNotFromPc => Verdict == Verdict.HiccupsNotFromPc;

    public string Headline { get; }

    public string Summary { get; }

    public string ChecksSummary { get; }

    /// <summary>Ready after a scan shows two tiles: hiccups, and optional improvements (or "all passed").</summary>
    public bool HasScanTiles { get; }

    /// <summary>Ready before a scan shows a line of text instead of the tiles.</summary>
    public bool ShowChecksLine => !HasScanTiles;

    public int HiccupCount { get; }

    public string ChecksTileLabel { get; }

    public string ChecksTileValue { get; }

    public IReadOnlyList<FindingRowViewModel> ToFix { get; }

    /// <summary>Warnings that don't stop a Ready verdict, listed as optional improvements.</summary>
    public IReadOnlyList<FindingRowViewModel> Improvements { get; }

    public bool HasImprovements => Improvements.Count > 0;

    /// <summary>Ready from the instant checks alone: the scan is the next step.</summary>
    public bool OfferScan { get; }

    /// <summary>After a scan, the Ready screen ends with Done instead of offering a scan.</summary>
    public bool ShowDoneOnly => !OfferScan;

    public string ScanButtonText { get; }

    /// <summary>A line about the scan: what to do next, why it couldn't run, or how precise it was.</summary>
    public string? ScanNote { get; }

    public bool HasScanNote => ScanNote is not null;

    /// <summary>After a scan that saw a stream: the whole scan as a strip, with a tick for each hiccup.</summary>
    public bool HasTimeline { get; }

    public IReadOnlyList<double> HiccupMarks { get; }

    /// <summary>How long the scan ran, shown at the end of the strip.</summary>
    public string TimelineLength { get; }

    [RelayCommand]
    private void StartScan() => _startScan();

    [RelayCommand]
    private void CheckAgain() => _checkAgain();

    [RelayCommand]
    private void Done() => _close();

    private static string Count(int n, string noun) => $"{n} {noun}{(n == 1 ? "" : "s")}";
}

/// <summary>One finding in a results list, with its severity stripe and guide button.</summary>
public partial class FindingRowViewModel(Finding finding, Action<string> openGuide) : ViewModelBase
{
    public string Title => UiText.KeepUnits(finding.Title);

    public string Description => UiText.KeepUnits(finding.Description);

    public bool IsBlocker => finding.Severity == Severity.Blocker;

    public bool IsWarning => finding.Severity == Severity.Warning;

    public bool IsPass => finding.Severity == Severity.Pass;

    /// <summary>A text tag beside the title, so colour isn't the only signal.</summary>
    public string? Tag => finding.Severity switch
    {
        Severity.Blocker => "MUST FIX",
        Severity.Warning => "COULD HELP",
        _ => null,
    };

    public bool HasTag => Tag is not null;

    public bool HasGuide => finding.Action == FindingAction.Guide;

    [RelayCommand]
    private void OpenGuide() => openGuide(finding.CheckId);
}
