using Avalonia;
using Avalonia.Media;
using System;

namespace DongleDoctor.App;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .With(new FontManagerOptions { DefaultFamilyName = Fonts.Body })
            .LogToTrace();
}

/// <summary>The bundled fonts: Atkinson Hyperlegible for text, Bricolage Grotesque for headings.</summary>
static class Fonts
{
    public const string Body = "avares://DongleDoctor/Assets/Fonts#Atkinson Hyperlegible";
}
