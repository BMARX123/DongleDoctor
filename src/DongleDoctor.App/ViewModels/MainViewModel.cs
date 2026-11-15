using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using DongleDoctor.Core.Checks;

namespace DongleDoctor.App.ViewModels;

/// <summary>Owns the wizard: which screen is showing.</summary>
public partial class MainViewModel : ViewModelBase
{
    private readonly Func<IReadOnlyList<ICheck>> _createChecks;
    private readonly Action _close;

    public MainViewModel(Func<IReadOnlyList<ICheck>> createChecks, Action close)
    {
        _createChecks = createChecks;
        _close = close;
        CurrentPage = new WelcomeViewModel(StartChecks);
    }

    [ObservableProperty]
    public partial ViewModelBase CurrentPage { get; set; }

    private void StartChecks()
    {
        var checks = new QuickChecksViewModel(_createChecks, ShowResult);
        CurrentPage = checks;
        _ = checks.RunAsync();
    }

    // There's no streaming scan yet, so the verdict comes from the instant checks alone.
    private void ShowResult(IReadOnlyList<Finding> findings) =>
        CurrentPage = new ResultViewModel(Scoring.Assess(findings), StartChecks, _close, Guides.Open);
}
