using DongleDoctor.Core.Usb;
using Windows.Win32;

namespace DongleDoctor.Platform.Windows.Usb;

/// <summary>Finds the dongle in the Windows device tree and walks up to its root hub.</summary>
public sealed class WindowsUsbTopology : IUsbTopology
{
    private static readonly string DonglePrefix = $@"USB\VID_{SteamFrameDongle.VendorId:X4}&PID_{SteamFrameDongle.ProductId:X4}\";

    /// <summary>The dongle's device instance ID, or null if it isn't plugged in.</summary>
    internal static string? FindDongleInstanceId() =>
        // The trailing backslash skips interface children of a composite device (…&MI_00\…).
        ConfigManager.PresentDeviceIds("USB").FirstOrDefault(d => d.StartsWith(DonglePrefix, StringComparison.OrdinalIgnoreCase));

    public DongleConnection? FindDongle()
    {
        var id = FindDongleInstanceId();
        if (id is null || ConfigManager.Locate(id) is not { } devInst)
        {
            return null;
        }

        var hubs = ParentHubs(devInst);
        var port = ConfigManager.GetUInt32(devInst, PInvoke.DEVPKEY_Device_Address);
        var speed = hubs.Count > 0 && port is { } p ? HubPort.GetSpeed(hubs[0].InstanceId, p) : UsbSpeed.Unknown;
        return new DongleConnection(id, ConfigManager.GetString(devInst, PInvoke.DEVPKEY_NAME) ?? id, speed, hubs,
            ConfigManager.GetProblemCode(devInst), hubs.Count > 0 ? Neighbors(id, hubs[^1].InstanceId) : []);
    }

    /// <summary>The other devices under the same root hub, i.e. on the same host controller.</summary>
    private static List<UsbNeighbor> Neighbors(string dongleId, string rootHubId)
    {
        var devices = new Dictionary<string, (string Name, bool IsHub)>(StringComparer.OrdinalIgnoreCase);
        var videoDevices = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in ConfigManager.PresentDeviceIds("USB"))
        {
            if (ConfigManager.Locate(id) is not { } devInst || !rootHubId.Equals(RootHubOf(id, devInst), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var compatible = ConfigManager.GetStringList(devInst, PInvoke.DEVPKEY_Device_CompatibleIds);
            if (compatible.Any(c => c.StartsWith(@"USB\Class_0E", StringComparison.OrdinalIgnoreCase)))
            {
                // A webcam's video function is usually an interface (…&MI_00\…) of a composite device; credit the device.
                videoDevices.Add(IsInterface(id) ? ConfigManager.GetString(devInst, PInvoke.DEVPKEY_Device_Parent) ?? id : id);
            }

            if (!IsInterface(id) && !id.Equals(dongleId, StringComparison.OrdinalIgnoreCase))
            {
                // Hubs report generic IDs such as USB\USB30_HUB rather than the hub class, so look for the hub interface.
                devices[id] = (ConfigManager.GetString(devInst, PInvoke.DEVPKEY_NAME) ?? id,
                    ConfigManager.InterfacePaths(PInvoke.GUID_DEVINTERFACE_USB_HUB, id).Count > 0);
            }
        }

        return [.. devices.Where(d => !d.Value.IsHub).Select(d => new UsbNeighbor(d.Key, d.Value.Name, videoDevices.Contains(d.Key)))];
    }

    private static bool IsInterface(string instanceId) => instanceId.Contains("&MI_", StringComparison.OrdinalIgnoreCase);

    /// <summary>The instance ID of the root hub above a device (or the device itself, if it's a root hub); null if there is none.</summary>
    private static string? RootHubOf(string instanceId, uint devInst)
    {
        var id = instanceId;
        uint? node = devInst;
        while (node is { } current && id.StartsWith(@"USB\", StringComparison.OrdinalIgnoreCase))
        {
            if (id.StartsWith(@"USB\ROOT_HUB", StringComparison.OrdinalIgnoreCase))
            {
                return id;
            }

            id = ConfigManager.GetString(current, PInvoke.DEVPKEY_Device_Parent) ?? "";
            node = ConfigManager.Locate(id);
        }

        return null;
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
