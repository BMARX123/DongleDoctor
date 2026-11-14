using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using DongleDoctor.Core.Checks;
using DongleDoctor.Core.Usb;

namespace DongleDoctor.App.ViewModels;

/// <summary>Owns the wizard: which screen is showing.</summary>
public partial class MainViewModel : ViewModelBase
{
    private readonly IUsbTopology _usb;
    private readonly Action _close;

    public MainViewModel(IUsbTopology usb, Action close)
    {
        _usb = usb;
        _close = close;
        CurrentPage = new WelcomeViewModel(StartChecks);
    }

    [ObservableProperty]
    public partial ViewModelBase CurrentPage { get; set; }

    private void StartChecks()
    {
        var checks = new QuickChecksViewModel(_usb, ShowResult);
        CurrentPage = checks;
        _ = checks.RunAsync();
    }

    // There's no streaming scan yet, so the verdict comes from the instant checks alone.
    private void ShowResult(IReadOnlyList<Finding> findings) =>
        CurrentPage = new ResultViewModel(Scoring.Assess(findings), StartChecks, _close, Guides.Open);
}
