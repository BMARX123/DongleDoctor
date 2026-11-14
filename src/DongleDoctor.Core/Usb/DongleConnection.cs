namespace DongleDoctor.Core.Usb;

/// <summary>Valve's Steam Frame USB Wi-Fi dongle (a Realtek RTL8832CU).</summary>
public static class SteamFrameDongle
{
    public const ushort VendorId = 0x28DE;
    public const ushort ProductId = 0x2432;
}

/// <summary>Negotiated USB speed, in the order of USB_DEVICE_SPEED plus SuperSpeedPlus.</summary>
public enum UsbSpeed
{
    Unknown,
    Low,
    Full,
    High,
    SuperSpeed,
    SuperSpeedPlus,
}

/// <summary>A hub between the dongle and the host controller.</summary>
/// <param name="IsRootHub">The host controller's own root hub.</param>
/// <param name="IsBuiltIn">
/// Windows places the hub in the PC's own device container, i.e. it's part of the PC rather than
/// something plugged in. Root hubs are always built in.
/// </param>
public sealed record UsbHub(string InstanceId, string Name, bool IsRootHub, bool IsBuiltIn);

/// <summary>Where and how the dongle is connected.</summary>
/// <param name="ParentHubs">Nearest hub first; the last entry is the root hub.</param>
/// <param name="ProblemCode">The device manager problem code; 0 means the device works.</param>
public sealed record DongleConnection(
    string InstanceId,
    string Name,
    UsbSpeed Speed,
    IReadOnlyList<UsbHub> ParentHubs,
    uint ProblemCode = 0)
{
    /// <summary>Hubs other than the root hub, nearest first.</summary>
    public IEnumerable<UsbHub> Hubs => ParentHubs.Where(h => !h.IsRootHub);
}

/// <summary>Reads the dongle's USB connection from the OS.</summary>
public interface IUsbTopology
{
    /// <summary>The dongle's connection, or null if no dongle is plugged in.</summary>
    DongleConnection? FindDongle();
}
