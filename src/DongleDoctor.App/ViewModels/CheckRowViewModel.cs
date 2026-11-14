using CommunityToolkit.Mvvm.ComponentModel;
using DongleDoctor.Core.Checks;

namespace DongleDoctor.App.ViewModels;

/// <summary>One row on the Quick checks screen: a spinner while running, then the finding.</summary>
public partial class CheckRowViewModel(string runningTitle) : ViewModelBase
{
    [ObservableProperty]
    public partial string Title { get; set; } = runningTitle;

    [ObservableProperty]
    public partial string Description { get; set; } = "Checking…";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRunning), nameof(IsGood), nameof(IsWarning), nameof(IsBlocker), nameof(Icon))]
    public partial Severity? Severity { get; set; }

    public bool IsRunning => Severity is null;

    // Info (e.g. "Skipped") reads as neutral rather than green.
    public bool IsGood => Severity is Core.Checks.Severity.Pass;

    public bool IsWarning => Severity is Core.Checks.Severity.Warning;

    public bool IsBlocker => Severity is Core.Checks.Severity.Blocker;

    public string Icon => Severity switch
    {
        null => "",
        Core.Checks.Severity.Pass => "✓",
        Core.Checks.Severity.Info => "i",
        _ => "!",
    };

    public void Complete(Finding finding)
    {
        Title = finding.Title;
        Description = finding.Description;
        Severity = finding.Severity;
    }
}
