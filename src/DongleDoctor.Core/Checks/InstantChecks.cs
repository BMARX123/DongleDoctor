using DongleDoctor.Core.Adapter;
using DongleDoctor.Core.Power;
using DongleDoctor.Core.Usb;

namespace DongleDoctor.Core.Checks;

/// <summary>Every instant check, in the order the Quick checks screen shows them. Each call reads the OS afresh.</summary>
public static class InstantChecks
{
    public static IReadOnlyList<ICheck> Create(IUsbTopology usb, IDongleDriverSettingsReader driver, IPowerSettingsReader power) =>
        [.. UsbChecks.Create(usb), .. AdapterChecks.Create(driver), .. PowerChecks.Create(power)];
}
