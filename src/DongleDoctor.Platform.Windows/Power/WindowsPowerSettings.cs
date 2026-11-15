using System.ComponentModel;
using System.Runtime.InteropServices;
using DongleDoctor.Core.Power;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace DongleDoctor.Platform.Windows.Power;

/// <summary>Reads the active power plan through powrprof. Reading needs no admin rights.</summary>
public sealed unsafe partial class WindowsPowerSettings : IPowerSettingsReader
{
    private static readonly Guid UsbSubgroup = new("2a737441-1930-4402-8d77-b2bebba308a3");
    private static readonly Guid UsbSelectiveSuspend = new("48e6b7a6-50f5-4782-a5d4-53bb8f07e226");
    private static readonly Guid WirelessSubgroup = new("19cbb8fa-5279-450e-9fac-8a3d5fedd0c1");
    private static readonly Guid WirelessPowerSavingMode = new("12bbebe6-58d6-4636-95bb-3217ef867c1a");

    public PowerSettings Read()
    {
        var scheme = ActiveScheme();
        var source = CurrentSource();
        var usb = ReadIndex(scheme, UsbSubgroup, UsbSelectiveSuspend, source);
        var wireless = ReadIndex(scheme, WirelessSubgroup, WirelessPowerSavingMode, source);
        return new PowerSettings(
            scheme,
            FriendlyName(scheme) ?? scheme.ToString(),
            source,
            PowerMode(),
            usb is null ? null : usb != 0,
            wireless is null ? null : (WirelessPowerSaving)wireless.Value);
    }

    /// <summary>The Windows 11 power mode laid over the plan, or null on Windows versions without power modes.</summary>
    private static Guid? PowerMode()
    {
        try
        {
            return PowerGetEffectiveOverlayScheme(out var overlay) == 0 ? overlay : null;
        }
        catch (EntryPointNotFoundException)
        {
            return null;
        }
    }

    // Not in CsWin32's metadata.
    [LibraryImport("powrprof.dll")]
    private static partial uint PowerGetEffectiveOverlayScheme(out Guid effectiveOverlayGuid);

    private static Guid ActiveScheme()
    {
        var error = PInvoke.PowerGetActiveScheme(null, out var scheme);
        if (error != WIN32_ERROR.ERROR_SUCCESS)
        {
            throw new Win32Exception((int)error, "PowerGetActiveScheme failed");
        }

        try
        {
            return *scheme;
        }
        finally
        {
            PInvoke.LocalFree((HLOCAL)scheme);
        }
    }

    // Laptops on battery use the plan's DC values; everything else uses AC.
    private static PowerSource CurrentSource() =>
        PInvoke.GetSystemPowerStatus(out var status) && status.ACLineStatus == 0 ? PowerSource.Battery : PowerSource.PluggedIn;

    /// <summary>The setting's value index, or null if this PC doesn't have the setting.</summary>
    private static uint? ReadIndex(Guid scheme, Guid subgroup, Guid setting, PowerSource source)
    {
        uint value;
        var error = source == PowerSource.Battery
            ? (WIN32_ERROR)PInvoke.PowerReadDCValueIndex(null, scheme, subgroup, setting, out value)
            : PInvoke.PowerReadACValueIndex(null, scheme, subgroup, setting, out value);
        return error == WIN32_ERROR.ERROR_SUCCESS ? value : null;
    }

    private static string? FriendlyName(Guid scheme)
    {
        uint size = 0;
        if (PInvoke.PowerReadFriendlyName(null, scheme, null, null, [], ref size) != WIN32_ERROR.ERROR_SUCCESS || size == 0)
        {
            return null;
        }

        var buffer = new byte[size];
        if (PInvoke.PowerReadFriendlyName(null, scheme, null, null, buffer, ref size) != WIN32_ERROR.ERROR_SUCCESS)
        {
            return null;
        }

        return System.Text.Encoding.Unicode.GetString(buffer, 0, (int)size).TrimEnd('\0');
    }
}
