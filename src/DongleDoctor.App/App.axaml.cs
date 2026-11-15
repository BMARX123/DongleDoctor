using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DongleDoctor.App.ViewModels;
using DongleDoctor.App.Views;
using DongleDoctor.Core.Checks;
using DongleDoctor.Core.Diagnostics;
using DongleDoctor.Platform.Windows;
using DongleDoctor.Platform.Windows.Adapter;
using DongleDoctor.Platform.Windows.Scan;
using DongleDoctor.Platform.Windows.Wifi;
using DongleDoctor.Platform.Windows.Power;
using DongleDoctor.Platform.Windows.Usb;

namespace DongleDoctor.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();
            window.DataContext = new MainViewModel(
                () => InstantChecks.Create(new WindowsUsbTopology(), new WindowsDongleDriverSettings(), new WindowsPowerSettings()),
                () => [new SteamLogProbe(), new IcmpProbe()],
                new WindowsWifiSurveyor(),
                () => Attention.Notify(window.TryGetPlatformHandle()?.Handle ?? 0),
                () => desktop.Shutdown(),
                new RunLog(RunLog.DefaultDirectory));
            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
