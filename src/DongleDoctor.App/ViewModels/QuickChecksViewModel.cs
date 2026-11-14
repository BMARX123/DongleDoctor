using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DongleDoctor.Core.Checks;
using DongleDoctor.Core.Usb;

namespace DongleDoctor.App.ViewModels;

/// <summary>
/// The Quick checks screen: runs the instant checks and shows each row as it finishes. The checks take
/// milliseconds, so it waits for the user to press See results rather than flashing past.
/// </summary>
public partial class QuickChecksViewModel(IUsbTopology usb, Action<IReadOnlyList<Finding>> showResults) : ViewModelBase
{
    private IReadOnlyList<Finding>? _findings;

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
        var checks = UsbChecks.Create(usb);
        foreach (var check in checks)
        {
            Rows.Add(new CheckRowViewModel(check.Name));
        }

        OnPropertyChanged(nameof(Progress));
        _findings = await Task.WhenAll(checks.Select(async (check, i) =>
        {
            var finding = await CheckRunner.RunSafeAsync(check);
            Rows[i].Complete(finding);
            Done++;
            return finding;
        }));
        IsFinished = true;
    }

    [RelayCommand(CanExecute = nameof(IsFinished))]
    private void SeeResults() => showResults(_findings!);
}
