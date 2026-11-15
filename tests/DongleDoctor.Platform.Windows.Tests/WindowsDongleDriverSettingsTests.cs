using DongleDoctor.Platform.Windows.Adapter;
using Xunit.Abstractions;

namespace DongleDoctor.Platform.Windows.Tests;

// Reads the real dongle's driver key, so it passes on any PC: with no dongle plugged in, Read returns null.
public class WindowsDongleDriverSettingsTests(ITestOutputHelper output)
{
    [Fact]
    public void Read_WhenPresent_HasTheRealtekSettings()
    {
        var settings = new WindowsDongleDriverSettings().Read();
        output.WriteLine(settings?.ToString() ?? "No dongle plugged in.");
        if (settings is null)
        {
            return;
        }

        Assert.NotNull(settings.AllowTurnOff);
        Assert.NotNull(settings.DriverPowerSaving);
        Assert.NotNull(settings.ForcedUsb2);
    }
}
