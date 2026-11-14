using DongleDoctor.Core.Checks;
using DongleDoctor.Core.Usb;

namespace DongleDoctor.Core.Tests;

public class UsbChecksTests
{
    private static readonly UsbHub RootHub = new(@"USB\ROOT_HUB30\1", "USB Root Hub (USB 3.0)", IsRootHub: true, IsBuiltIn: true);

    private static DongleConnection Dongle(UsbSpeed speed = UsbSpeed.SuperSpeed, uint problem = 0, params UsbHub[] hubs) =>
        new(@"USB\VID_28DE&PID_2432\X", "Realtek 8832CU", speed, [.. hubs, RootHub], problem);

    [Fact]
    public void Presence_NoDongle_IsBlocker()
    {
        var f = UsbChecks.EvaluatePresence(null);
        Assert.Equal(Severity.Blocker, f.Severity);
        Assert.Equal(FindingAction.Guide, f.Action);
    }

    [Fact]
    public void Presence_DeviceProblem_IsBlockerWithCode()
    {
        var f = UsbChecks.EvaluatePresence(Dongle(problem: 43));
        Assert.Equal(Severity.Blocker, f.Severity);
        Assert.Contains("43", f.Details);
    }

    [Fact]
    public void Presence_Working_Passes() =>
        Assert.Equal(Severity.Pass, UsbChecks.EvaluatePresence(Dongle()).Severity);

    [Theory]
    [InlineData(UsbSpeed.SuperSpeedPlus, Severity.Pass)]
    [InlineData(UsbSpeed.SuperSpeed, Severity.Pass)]
    [InlineData(UsbSpeed.High, Severity.Warning)]
    [InlineData(UsbSpeed.Full, Severity.Warning)]
    [InlineData(UsbSpeed.Unknown, Severity.Info)]
    public void Speed_SuperSpeedIsEnough(UsbSpeed speed, Severity expected) =>
        Assert.Equal(expected, UsbChecks.EvaluateSpeed(Dongle(speed)).Severity);

    [Fact]
    public void Hub_DirectOnRootHub_Passes()
    {
        var f = UsbChecks.EvaluateHub(Dongle());
        Assert.Equal(Severity.Pass, f.Severity);
        Assert.Equal("Plugged straight into your PC", f.Title);
    }

    [Fact]
    public void Hub_PluggedInHub_Warns()
    {
        var hub = new UsbHub(@"USB\VID_2109&PID_0822\1", "Generic SuperSpeed USB Hub", IsRootHub: false, IsBuiltIn: false);
        var f = UsbChecks.EvaluateHub(Dongle(hubs: hub));
        Assert.Equal(Severity.Warning, f.Severity);
        Assert.Equal(FindingAction.Guide, f.Action);
    }

    [Fact]
    public void Hub_BuiltInHub_Passes()
    {
        var hub = new UsbHub(@"USB\VID_2109&PID_0822\1", "Generic SuperSpeed USB Hub", IsRootHub: false, IsBuiltIn: true);
        Assert.Equal(Severity.Pass, UsbChecks.EvaluateHub(Dongle(hubs: hub)).Severity);
    }

    [Fact]
    public void Hub_PluggedInHubBehindBuiltInHub_Warns()
    {
        var builtIn = new UsbHub(@"USB\VID_1\1", "Internal hub", IsRootHub: false, IsBuiltIn: true);
        var external = new UsbHub(@"USB\VID_2\1", "Desk hub", IsRootHub: false, IsBuiltIn: false);
        Assert.Equal(Severity.Warning, UsbChecks.EvaluateHub(Dongle(UsbSpeed.SuperSpeed, 0, external, builtIn)).Severity);
    }

    [Theory]
    [InlineData("usb.speed")]
    [InlineData("usb.hub")]
    public async Task NoDongle_DependentChecksSkip(string id)
    {
        var check = UsbChecks.Create(new FakeTopology(null)).Single(c => c.Id == id);
        var f = await check.RunAsync();
        Assert.Equal(Severity.Info, f.Severity);
    }

    [Fact]
    public async Task Create_ReadsTopologyOncePerRun()
    {
        var topology = new FakeTopology(Dongle());
        foreach (var check in UsbChecks.Create(topology))
        {
            await check.RunAsync();
        }

        Assert.Equal(1, topology.Reads);
    }

    private sealed class FakeTopology(DongleConnection? dongle) : IUsbTopology
    {
        public int Reads { get; private set; }

        public DongleConnection? FindDongle()
        {
            Reads++;
            return dongle;
        }
    }
}
