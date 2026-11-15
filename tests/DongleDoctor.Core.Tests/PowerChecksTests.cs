using DongleDoctor.Core.Checks;
using DongleDoctor.Core.Power;

namespace DongleDoctor.Core.Tests;

public class PowerChecksTests
{
    private static PowerSettings Settings(
        Guid? plan = null,
        bool? usbSuspend = false,
        WirelessPowerSaving? wireless = WirelessPowerSaving.MaximumPerformance,
        PowerSource source = PowerSource.PluggedIn,
        Guid? powerMode = null) =>
        new(plan ?? PowerPlans.HighPerformance, "A plan", source, powerMode, usbSuspend, wireless);

    [Fact]
    public void Plan_PowerSaver_Warns()
    {
        var f = PowerChecks.EvaluatePlan(Settings(PowerPlans.PowerSaver));
        Assert.Equal(Severity.Warning, f.Severity);
        Assert.Equal("power.plan", f.CheckId);
    }

    [Fact]
    public void Plan_Balanced_Warns() =>
        Assert.Equal(Severity.Warning, PowerChecks.EvaluatePlan(Settings(PowerPlans.Balanced)).Severity);

    [Fact]
    public void Plan_BalancedWithBestPerformanceMode_Passes() =>
        Assert.Equal(Severity.Pass,
            PowerChecks.EvaluatePlan(Settings(PowerPlans.Balanced, powerMode: PowerPlans.BestPerformanceMode)).Severity);

    [Theory]
    [InlineData("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c")] // High performance
    [InlineData("e9a42b02-d5df-448d-aa00-03f14749eb61")] // Ultimate performance
    [InlineData("11111111-2222-3333-4444-555555555555")] // A custom plan
    public void Plan_OtherPlans_Pass(string plan) =>
        Assert.Equal(Severity.Pass, PowerChecks.EvaluatePlan(Settings(new Guid(plan))).Severity);

    [Theory]
    [InlineData(true, Severity.Warning)]
    [InlineData(false, Severity.Pass)]
    [InlineData(null, Severity.Pass)]
    public void UsbSuspend(bool? enabled, Severity expected) =>
        Assert.Equal(expected, PowerChecks.EvaluateUsbSuspend(Settings(usbSuspend: enabled)).Severity);

    [Theory]
    [InlineData(WirelessPowerSaving.MaximumPerformance, Severity.Pass)]
    [InlineData(WirelessPowerSaving.Low, Severity.Warning)]
    [InlineData(WirelessPowerSaving.Medium, Severity.Warning)]
    [InlineData(WirelessPowerSaving.Maximum, Severity.Warning)]
    [InlineData(null, Severity.Pass)]
    public void Wireless(WirelessPowerSaving? mode, Severity expected) =>
        Assert.Equal(expected, PowerChecks.EvaluateWireless(Settings(wireless: mode)).Severity);

    [Fact]
    public void Details_NameThePowerSource() =>
        Assert.Contains("battery", PowerChecks.EvaluateWireless(Settings(source: PowerSource.Battery)).Details);

    [Fact]
    public async Task Create_ReadsSettingsOncePerRun()
    {
        var reader = new FakeReader(Settings());
        foreach (var check in PowerChecks.Create(reader))
        {
            await check.RunAsync();
        }

        Assert.Equal(1, reader.Reads);
    }

    private sealed class FakeReader(PowerSettings settings) : IPowerSettingsReader
    {
        public int Reads { get; private set; }

        public PowerSettings Read()
        {
            Reads++;
            return settings;
        }
    }
}
