using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using DongleDoctor.Core.Checks;

namespace DongleDoctor.App.ViewModels;

/// <summary>The results screen for all three verdicts: Ready, Not ready, and hiccups that don't come from the PC.</summary>
public partial class ResultViewModel : ViewModelBase
{
    private readonly Action _checkAgain;
    private readonly Action _close;

    public ResultViewModel(Assessment assessment, Action checkAgain, Action close, Action<string> openGuide)
    {
        _checkAgain = checkAgain;
        _close = close;
        Verdict = assessment.Verdict;
        ChecksSummary = $"{assessment.ChecksPassed} of {assessment.ChecksRun}";
        ToFix = [.. assessment.ToFix.Select(f => new FindingRowViewModel(f, openGuide))];
        Improvements = [.. assessment.Improvements.Select(f => new FindingRowViewModel(f, openGuide))];

        var scan = assessment.Scan;
        var hiccups = scan is null ? null : $"{Count(scan.Hiccups, "hiccup")} in {Count((int)Math.Round(scan.Duration.TotalMinutes), "minute")}.";
        (Headline, Summary) = Verdict switch
        {
            Verdict.NotReady => ("Not ready yet", $"{hiccups} {Count(ToFix.Count, "thing")} to fix.".TrimStart()),
            Verdict.HiccupsNotFromPc => ("Your PC looks fine, but you had hiccups", $"{hiccups} Nothing on your PC lined up with them."),
            _ => ("You're ready", scan is null
                ? "Your dongle is connected the way it should be."
                : $"{hiccups} Your connection stayed steady."),
        };
    }

    public Verdict Verdict { get; }

    public bool IsReady => Verdict == Verdict.Ready;

    public bool IsNotReady => Verdict == Verdict.NotReady;

    public bool IsNotFromPc => Verdict == Verdict.HiccupsNotFromPc;

    public string Headline { get; }

    public string Summary { get; }

    public string ChecksSummary { get; }

    public IReadOnlyList<FindingRowViewModel> ToFix { get; }

    /// <summary>Warnings that don't stop a Ready verdict, listed as optional improvements.</summary>
    public IReadOnlyList<FindingRowViewModel> Improvements { get; }

    public bool HasImprovements => Improvements.Count > 0;

    [RelayCommand]
    private void CheckAgain() => _checkAgain();

    [RelayCommand]
    private void Done() => _close();

    private static string Count(int n, string noun) => $"{n} {noun}{(n == 1 ? "" : "s")}";
}

/// <summary>One finding in a results list, with its severity stripe and guide button.</summary>
public partial class FindingRowViewModel(Finding finding, Action<string> openGuide) : ViewModelBase
{
    public string Title => finding.Title;

    public string Description => finding.Description;

    public bool IsBlocker => finding.Severity == Severity.Blocker;

    public bool IsWarning => finding.Severity == Severity.Warning;

    public bool HasGuide => finding.Action == FindingAction.Guide;

    [RelayCommand]
    private void OpenGuide() => openGuide(finding.CheckId);
}
