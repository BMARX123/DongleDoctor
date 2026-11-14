using DongleDoctor.Core.Usb;
using DongleDoctor.Platform.Windows.Usb;
using Xunit.Abstractions;

namespace DongleDoctor.Platform.Windows.Tests;

// These read the real device tree, so they pass on any PC: with no dongle plugged in, FindDongle returns null.
public class WindowsUsbTopologyTests(ITestOutputHelper output)
{
    [Fact]
    public void SplitMultiString_StopsAtDoubleNull() =>
        Assert.Equal(["A", "B"], ConfigManager.SplitMultiString("A\0B\0\0"));

    [Fact]
    public void PresentUsbDevices_IncludesARootHub() =>
        Assert.Contains(ConfigManager.PresentDeviceIds("USB"), id => id.StartsWith(@"USB\ROOT_HUB", StringComparison.OrdinalIgnoreCase));

    [Fact]
    public void FindDongle_WhenPresent_ChainEndsAtBuiltInRootHub()
    {
        var dongle = new WindowsUsbTopology().FindDongle();
        output.WriteLine(dongle is null ? "No dongle plugged in." : Describe(dongle));
        if (dongle is null)
        {
            return;
        }

        Assert.NotEmpty(dongle.ParentHubs);
        Assert.True(dongle.ParentHubs[^1].IsRootHub);
        Assert.True(dongle.ParentHubs[^1].IsBuiltIn);
        Assert.Single(dongle.ParentHubs, h => h.IsRootHub);
        Assert.NotEqual(UsbSpeed.Unknown, dongle.Speed);
    }

    private static string Describe(DongleConnection d) =>
        $"{d.Name} [{d.InstanceId}] speed={d.Speed} problem={d.ProblemCode}{Environment.NewLine}" +
        string.Join(Environment.NewLine, d.ParentHubs.Select(h => $"  ← {h.Name} [{h.InstanceId}] root={h.IsRootHub} builtIn={h.IsBuiltIn}"));
}
