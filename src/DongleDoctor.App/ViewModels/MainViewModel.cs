using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using DongleDoctor.Core.Checks;
using DongleDoctor.Core.Diagnostics;
using DongleDoctor.Core.Scan;

namespace DongleDoctor.App.ViewModels;

/// <summary>Owns the wizard: which screen is showing.</summary>
public partial class MainViewModel : ViewModelBase
{
    private readonly Func<IReadOnlyList<ICheck>> _createChecks;
    private readonly Func<IReadOnlyList<IProbe>> _createProbes;
    private readonly Func<bool> _scanFinished;
    private readonly Action _close;
    private readonly RunLog _log;
    private IReadOnlyList<Finding> _findings = [];

    /// <param name="scanFinished">Gets the user's attention: they're in the headset when a scan ends. Returns whether the sound played.</param>
    public MainViewModel(
        Func<IReadOnlyList<ICheck>> createChecks, Func<IReadOnlyList<IProbe>> createProbes, Func<bool> scanFinished, Action close, RunLog log)
    {
        _createChecks = createChecks;
        _createProbes = createProbes;
        _scanFinished = scanFinished;
        _close = close;
        _log = log;
        CurrentPage = new WelcomeViewModel(StartChecks);
    }

    [ObservableProperty]
    public partial ViewModelBase CurrentPage { get; set; }

    private void StartChecks()
    {
        QuickChecksViewModel? checks = null;
        checks = new QuickChecksViewModel(_createChecks, findings =>
        {
            _findings = findings;
            _log.Checks(findings, DateTime.Now);

            // Passing checks lead straight on to the scan; Back returns to the checklist. Anything that stops a Ready
            // verdict goes to the result first, since it should be fixed before scanning.
            if (Scoring.Assess(findings).Verdict == Verdict.Ready)
            {
                CurrentPage = new StartScanViewModel(StartScan, () => CurrentPage = checks!);
            }
            else
            {
                ShowResult(null);
            }
        });
        CurrentPage = checks;
        _ = checks.RunAsync();
    }

    private void ShowStartScan() => CurrentPage = new StartScanViewModel(StartScan, () => ShowResult(null));

    private void StartScan(TimeSpan duration)
    {
        var started = DateTime.Now;
        var scanning = new ScanningViewModel(new ScanSession(_createProbes()), duration, (report, stoppedEarly) =>
        {
            bool? chimePlayed = stoppedEarly ? null : _scanFinished();
            _log.Scan(started, duration, stoppedEarly, report, chimePlayed);
            ShowResult(report);
        });
        CurrentPage = scanning;
        _ = scanning.RunAsync();
    }

    // A scan that never saw a stream can't judge anything, so the verdict falls back to the instant checks.
    private void ShowResult(ScanReport? scan) =>
        CurrentPage = new ResultViewModel(
            Scoring.Assess(_findings, scan is { StreamDetected: true } ? scan.Summary : null),
            scan, StartChecks, ShowStartScan, _close, Guides.Open);
}
