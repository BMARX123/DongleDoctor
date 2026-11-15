using DongleDoctor.Platform.Windows.Power;
using Xunit.Abstractions;

namespace DongleDoctor.Platform.Windows.Tests;

// Reads the real active power plan, so it passes on any PC.
public class WindowsPowerSettingsTests(ITestOutputHelper output)
{
    [Fact]
    public void Read_ReturnsTheActivePlan()
    {
        var settings = new WindowsPowerSettings().Read();
        output.WriteLine(settings.ToString());

        Assert.NotEqual(Guid.Empty, settings.PlanId);
        Assert.False(string.IsNullOrWhiteSpace(settings.PlanName));

        // Every Windows PC has the USB selective suspend setting.
        Assert.NotNull(settings.UsbSelectiveSuspend);
    }
}
