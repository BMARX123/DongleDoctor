using CommunityToolkit.Mvvm.ComponentModel;
using DongleDoctor.Core.Usb;

namespace DongleDoctor.App.ViewModels;

/// <summary>Owns the wizard: which screen is showing.</summary>
public partial class MainViewModel : ViewModelBase
{
    private readonly IUsbTopology _usb;

    public MainViewModel(IUsbTopology usb)
    {
        _usb = usb;
        CurrentPage = new WelcomeViewModel(StartChecks);
    }

    [ObservableProperty]
    public partial ViewModelBase CurrentPage { get; set; }

    private void StartChecks()
    {
        var checks = new QuickChecksViewModel(_usb);
        CurrentPage = checks;
        _ = checks.RunAsync();
    }
}
