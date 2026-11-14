using DongleDoctor.Core.Usb;
using Windows.Win32;

namespace DongleDoctor.Platform.Windows.Usb;

/// <summary>Finds the dongle in the Windows device tree and walks up to its root hub.</summary>
public sealed class WindowsUsbTopology : IUsbTopology
{
    private static readonly string DonglePrefix = $@"USB\VID_{SteamFrameDongle.VendorId:X4}&PID_{SteamFrameDongle.ProductId:X4}\";

    public DongleConnection? FindDongle()
    {
        // The trailing backslash skips interface children of a composite device (…&MI_00\…).
        var id = ConfigManager.PresentDeviceIds("USB").FirstOrDefault(d => d.StartsWith(DonglePrefix, StringComparison.OrdinalIgnoreCase));
        if (id is null || ConfigManager.Locate(id) is not { } devInst)
        {
            return null;
        }

        var hubs = ParentHubs(devInst);
        var port = ConfigManager.GetUInt32(devInst, PInvoke.DEVPKEY_Device_Address);
        var speed = hubs.Count > 0 && port is { } p ? HubPort.GetSpeed(hubs[0].InstanceId, p) : UsbSpeed.Unknown;
        return new DongleConnection(id, ConfigManager.GetString(devInst, PInvoke.DEVPKEY_NAME) ?? id, speed, hubs,
            ConfigManager.GetProblemCode(devInst));
    }

    private static List<UsbHub> ParentHubs(uint devInst)
    {
        var hubs = new List<UsbHub>();
        var parentId = ConfigManager.GetString(devInst, PInvoke.DEVPKEY_Device_Parent);
        // Stop at the host controller (PCI\…), which is the root hub's parent.
        while (parentId is not null && parentId.StartsWith(@"USB\", StringComparison.OrdinalIgnoreCase)
            && ConfigManager.Locate(parentId) is { } parent)
        {
            var isRoot = parentId.StartsWith(@"USB\ROOT_HUB", StringComparison.OrdinalIgnoreCase);
            hubs.Add(new UsbHub(
                parentId,
                ConfigManager.GetString(parent, PInvoke.DEVPKEY_NAME) ?? parentId,
                isRoot,
                isRoot || ConfigManager.GetBoolean(parent, PInvoke.DEVPKEY_Device_InLocalMachineContainer) == true));
            parentId = ConfigManager.GetString(parent, PInvoke.DEVPKEY_Device_Parent);
        }

        return hubs;
    }
}
