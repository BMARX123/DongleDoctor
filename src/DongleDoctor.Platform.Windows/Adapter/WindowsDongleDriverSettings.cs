using DongleDoctor.Core.Adapter;
using Microsoft.Win32;

namespace DongleDoctor.Platform.Windows.Adapter;

/// <summary>
/// Reads the dongle's driver settings from its network-class registry key, the same values Device Manager's
/// Advanced and Power Management tabs show. Reading needs no admin rights.
/// </summary>
public sealed class WindowsDongleDriverSettings : IDongleDriverSettingsReader
{
    // NDIS_DEVICE_DISABLE_PM: set when "Allow the computer to turn off this device" is unticked.
    private const int DisablePowerManagement = 0x8;

    public DongleDriverSettings? Read()
    {
        using var key = DongleDriverKey.Open(out var driverKey);
        return key is null ? null : Evaluate(key, driverKey);
    }

    private static DongleDriverSettings Evaluate(RegistryKey key, string driverKey)
    {
        // A missing PnPCapabilities means Windows' default: the device may be turned off.
        var pnpCapabilities = key.GetValue("PnPCapabilities") is int caps ? caps : 0;
        var lps = ReadInt(key, "LpsEn");
        var ips = ReadInt(key, "IpsEn");
        var usbMode = key.GetValue("ForcedUsbMode") as string;
        var usbModeName = string.IsNullOrEmpty(usbMode) ? null : EnumText(key, "ForcedUsbMode", usbMode);

        return new DongleDriverSettings(
            AllowTurnOff: (pnpCapabilities & DisablePowerManagement) == 0,
            DriverPowerSaving: lps is null && ips is null ? null : lps > 0 || ips > 0,
            ForcedUsb2: usbModeName?.Contains("2.0", StringComparison.Ordinal) == true,
            Details: $"{driverKey}: PnPCapabilities={pnpCapabilities}, LpsEn={lps?.ToString() ?? "-"}, IpsEn={ips?.ToString() ?? "-"}, " +
                $"ForcedUsbMode={(string.IsNullOrEmpty(usbMode) ? "Auto" : $"{usbMode} ({usbModeName ?? "?"})")}");
    }

    // NDIS advanced properties are usually strings, but some drivers write DWORDs.
    private static int? ReadInt(RegistryKey key, string name) => key.GetValue(name) switch
    {
        int i => i,
        string s when int.TryParse(s, out var i) => i,
        _ => null,
    };

    /// <summary>The display text Device Manager shows for an enum setting's value, from the driver's Ndi\params list.</summary>
    private static string? EnumText(RegistryKey key, string name, string value)
    {
        using var options = key.OpenSubKey($@"Ndi\params\{name}\enum");
        return options?.GetValue(value) as string;
    }
}
