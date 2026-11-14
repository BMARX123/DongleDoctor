using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DongleDoctor.Core.Checks;
using DongleDoctor.Core.Usb;

namespace DongleDoctor.App.ViewModels;

/// <summary>The Quick checks screen: runs the instant checks and shows each row as it finishes.</summary>
public partial class QuickChecksViewModel(IUsbTopology usb) : ViewModelBase
{
    public ObservableCollection<CheckRowViewModel> Rows { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Progress))]
    public partial int Done { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckAgainCommand))]
    public partial bool IsRunning { get; set; }

    public string Progress => $"{Done} of {Rows.Count} checked";

    public async Task RunAsync()
    {
        IsRunning = true;
        Rows.Clear();
        Done = 0;
        // A fresh set of checks re-reads the USB tree, so moving the dongle and checking again works.
        var checks = UsbChecks.Create(usb);
        foreach (var check in checks)
        {
            Rows.Add(new CheckRowViewModel(check.Name));
        }

        OnPropertyChanged(nameof(Progress));
        await Task.WhenAll(checks.Select(async (check, i) =>
        {
            Rows[i].Complete(await CheckRunner.RunSafeAsync(check));
            Done++;
        }));
        IsRunning = false;
    }

    [RelayCommand(CanExecute = nameof(CanCheckAgain))]
    private Task CheckAgain() => RunAsync();

    private bool CanCheckAgain() => !IsRunning;
}
