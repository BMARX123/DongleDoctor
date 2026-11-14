using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DongleDoctor.App.ViewModels;
using DongleDoctor.App.Views;
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
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel(new WindowsUsbTopology()),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
