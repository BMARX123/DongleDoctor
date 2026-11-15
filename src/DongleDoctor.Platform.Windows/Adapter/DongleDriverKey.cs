using DongleDoctor.Platform.Windows.Usb;
using Microsoft.Win32;
using Windows.Win32;

namespace DongleDoctor.Platform.Windows.Adapter;

/// <summary>The dongle's network-class registry key, which holds its driver settings. Readable without admin rights.</summary>
internal static class DongleDriverKey
{
    private const string ClassRoot = @"SYSTEM\CurrentControlSet\Control\Class\";

    /// <summary>Opens the key read-only, or returns null if no dongle is plugged in.</summary>
    /// <param name="driverKey">The key's name under the Class root, e.g. "{4d36e972-e325-11ce-bfc1-08002be10318}\0001".</param>
    public static RegistryKey? Open(out string driverKey)
    {
        driverKey = "";
        if (WindowsUsbTopology.FindDongleInstanceId() is not { } id || ConfigManager.Locate(id) is not { } devInst)
        {
            return null;
        }

        driverKey = ConfigManager.GetString(devInst, PInvoke.DEVPKEY_Device_Driver)
            ?? throw new InvalidOperationException($"{id} has no driver key");
        return Registry.LocalMachine.OpenSubKey(ClassRoot + driverKey)
            ?? throw new InvalidOperationException($"Driver key {driverKey} not found");
    }
}
