using DongleDoctor.Core.Adapter;

namespace DongleDoctor.Core.Checks;

/// <summary>
/// The instant checks on the dongle's driver settings. They share one read per run;
/// call <see cref="Create"/> again for a fresh read.
/// </summary>
public static class AdapterChecks
{
    public static IReadOnlyList<ICheck> Create(IDongleDriverSettingsReader reader)
    {
        var settings = new Lazy<DongleDriverSettings?>(reader.Read, LazyThreadSafetyMode.ExecutionAndPublication);
        return
        [
            new SharedReadCheck<DongleDriverSettings?>("adapter.power", "Checking the dongle's power settings", settings, EvaluatePower),
            new SharedReadCheck<DongleDriverSettings?>("adapter.usb-mode", "Checking the dongle's USB mode", settings, EvaluateUsbMode),
        ];
    }

    internal static Finding EvaluatePower(DongleDriverSettings? settings)
    {
        const string id = "adapter.power";
        if (settings is null)
        {
            return UsbChecks.Skipped(id);
        }

        if (settings.AllowTurnOff == true)
        {
            return new Finding(id, Severity.Warning, "Windows can turn off your dongle",
                "In Device Manager, open your dongle's Power Management tab and untick \"Allow the computer to turn off this device to save power\".",
                Details: settings.Details);
        }

        if (settings.DriverPowerSaving == true)
        {
            return new Finding(id, Severity.Warning, "Your dongle's power saving is on",
                "Something turned on power saving in your dongle's driver, which can make the link pause. To restore Valve's settings, uninstall the dongle in Device Manager, then unplug it and plug it back in.",
                Details: settings.Details);
        }

        return new Finding(id, Severity.Pass, "Dongle power saving is off", "Your dongle stays fully awake", Details: settings.Details);
    }

    internal static Finding EvaluateUsbMode(DongleDriverSettings? settings)
    {
        const string id = "adapter.usb-mode";
        if (settings is null)
        {
            return UsbChecks.Skipped(id);
        }

        return settings.ForcedUsb2 == true
            ? new Finding(id, Severity.Warning, "Your dongle is set to USB 2.0",
                "In Device Manager, open your dongle's Advanced tab and set \"USB Mode\" to Auto.",
                Details: settings.Details)
            : new Finding(id, Severity.Pass, "Dongle USB mode is fine", "Not limited to USB 2.0", Details: settings.Details);
    }
}
