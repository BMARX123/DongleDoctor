using DongleDoctor.Core.Adapter;
using DongleDoctor.Core.Checks;

namespace DongleDoctor.Core.Tests;

public class AdapterChecksTests
{
    private static DongleDriverSettings Settings(bool? allowTurnOff = false, bool? driverPowerSaving = false, bool? forcedUsb2 = false) =>
        new(allowTurnOff, driverPowerSaving, forcedUsb2, "details");

    [Theory]
    [InlineData(false, false, Severity.Pass)]
    [InlineData(null, null, Severity.Pass)]
    [InlineData(true, false, Severity.Warning)]
    [InlineData(false, true, Severity.Warning)]
    public void Power(bool? allowTurnOff, bool? driverPowerSaving, Severity expected) =>
        Assert.Equal(expected, AdapterChecks.EvaluatePower(Settings(allowTurnOff, driverPowerSaving)).Severity);

    [Fact]
    public void Power_TurnOffAllowed_PointsAtThePowerManagementTab() =>
        Assert.Contains("Power Management", AdapterChecks.EvaluatePower(Settings(allowTurnOff: true)).Description);

    [Theory]
    [InlineData(true, Severity.Warning)]
    [InlineData(false, Severity.Pass)]
    [InlineData(null, Severity.Pass)]
    public void UsbMode(bool? forcedUsb2, Severity expected) =>
        Assert.Equal(expected, AdapterChecks.EvaluateUsbMode(Settings(forcedUsb2: forcedUsb2)).Severity);

    [Theory]
    [InlineData("adapter.power")]
    [InlineData("adapter.usb-mode")]
    public async Task NoDongle_Skips(string id)
    {
        var check = AdapterChecks.Create(new FakeReader(null)).Single(c => c.Id == id);
        Assert.Equal(Severity.Info, (await check.RunAsync()).Severity);
    }

    [Fact]
    public async Task Create_ReadsSettingsOncePerRun()
    {
        var reader = new FakeReader(Settings());
        foreach (var check in AdapterChecks.Create(reader))
        {
            await check.RunAsync();
        }

        Assert.Equal(1, reader.Reads);
    }

    private sealed class FakeReader(DongleDriverSettings? settings) : IDongleDriverSettingsReader
    {
        public int Reads { get; private set; }

        public DongleDriverSettings? Read()
        {
            Reads++;
            return settings;
        }
    }
}
