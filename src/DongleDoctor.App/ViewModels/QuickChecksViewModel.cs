using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DongleDoctor.Core.Checks;

namespace DongleDoctor.App.ViewModels;

/// <summary>
/// The Quick checks screen: runs the instant checks and reveals the rows one at a time. The checks take
/// milliseconds, so the reveal is paced to be readable, and the screen waits for the user to continue.
/// </summary>
public partial class QuickChecksViewModel(Func<IReadOnlyList<ICheck>> createChecks, Action<IReadOnlyList<Finding>> showResults) : ViewModelBase
{
    /// <summary>The least time each row spends checking, so the user can follow the list.</summary>
    public static readonly TimeSpan RowPace = TimeSpan.FromMilliseconds(350);

    private IReadOnlyList<Finding>? _findings;

    /// <summary>"Continue" leads on to the scan; "See results" leads to what needs fixing.</summary>
    [ObservableProperty]
    public partial string ContinueText { get; set; } = "Continue";

    public ObservableCollection<CheckRowViewModel> Rows { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SeeResultsCommand))]
    public partial bool IsFinished { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Progress))]
    public partial int Done { get; set; }

    public string Progress => $"{Done} of {Rows.Count} checked";

    public async Task RunAsync()
    {
        var checks = createChecks();
        foreach (var check in checks)
        {
            Rows.Add(new CheckRowViewModel(check.Name));
        }

        OnPropertyChanged(nameof(Progress));
        var running = checks.Select(c => CheckRunner.RunSafeAsync(c)).ToList();
        var findings = new List<Finding>();
        for (var i = 0; i < running.Count; i++)
        {
            await Task.WhenAll(running[i], Task.Delay(RowPace));
            findings.Add(await running[i]);
            Rows[i].Complete(findings[i]);
            Done++;
        }

        _findings = findings;
        ContinueText = Scoring.Assess(findings).Verdict == Verdict.Ready ? "Continue" : "See results";
        IsFinished = true;
    }

    [RelayCommand(CanExecute = nameof(IsFinished))]
    private void SeeResults() => showResults(_findings!);
}
