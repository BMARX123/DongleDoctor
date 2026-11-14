using System.Runtime.InteropServices;
using DongleDoctor.Core.Usb;
using Windows.Win32;
using Windows.Win32.Devices.Usb;
using Windows.Win32.Storage.FileSystem;

namespace DongleDoctor.Platform.Windows.Usb;

/// <summary>Reads what a USB hub knows about the device on one of its ports.</summary>
internal static unsafe class HubPort
{
    private const uint GenericWrite = 0x40000000;

    // Room for the pipe list that follows the fixed part of the struct; the dongle opens a handful of pipes.
    private const int MaxPipes = 32;

    /// <summary>The negotiated speed of the device on <paramref name="port"/> of the hub <paramref name="hubInstanceId"/>.</summary>
    public static UsbSpeed GetSpeed(string hubInstanceId, uint port)
    {
        var path = ConfigManager.InterfacePaths(PInvoke.GUID_DEVINTERFACE_USB_HUB, hubInstanceId).FirstOrDefault();
        if (path is null)
        {
            return UsbSpeed.Unknown;
        }

        using var hub = PInvoke.CreateFile(path, GenericWrite, FILE_SHARE_MODE.FILE_SHARE_WRITE, null,
            FILE_CREATION_DISPOSITION.OPEN_EXISTING, 0, null);
        if (hub.IsInvalid)
        {
            return UsbSpeed.Unknown;
        }

        // For compatibility the EX IOCTL reports SuperSpeed devices as high speed; only the V2 flags tell them apart
        // (the same approach as Microsoft's USBView sample).
        if (GetV2Flags(hub, port) is { } flags)
        {
            if (flags.DeviceIsOperatingAtSuperSpeedPlusOrHigher)
            {
                return UsbSpeed.SuperSpeedPlus;
            }

            if (flags.DeviceIsOperatingAtSuperSpeedOrHigher)
            {
                return UsbSpeed.SuperSpeed;
            }
        }

        var info = new byte[sizeof(USB_NODE_CONNECTION_INFORMATION_EX) + MaxPipes * sizeof(USB_PIPE_INFO)];
        var header = info.AsSpan(0, sizeof(USB_NODE_CONNECTION_INFORMATION_EX));
        MemoryMarshal.Cast<byte, USB_NODE_CONNECTION_INFORMATION_EX>(header)[0].ConnectionIndex = port;
        if (!PInvoke.DeviceIoControl(hub, PInvoke.IOCTL_USB_GET_NODE_CONNECTION_INFORMATION_EX, info, info, out _))
        {
            return UsbSpeed.Unknown;
        }

        // USB_DEVICE_SPEED: 0 low, 1 full, 2 high, 3 SuperSpeed.
        return MemoryMarshal.Cast<byte, USB_NODE_CONNECTION_INFORMATION_EX>(header)[0].Speed switch
        {
            0 => UsbSpeed.Low,
            1 => UsbSpeed.Full,
            2 => UsbSpeed.High,
            3 => UsbSpeed.SuperSpeed,
            _ => UsbSpeed.Unknown,
        };
    }

    private static USB_NODE_CONNECTION_INFORMATION_EX_V2_FLAGS? GetV2Flags(SafeHandle hub, uint port)
    {
        var v2 = new USB_NODE_CONNECTION_INFORMATION_EX_V2
        {
            ConnectionIndex = port,
            Length = (uint)sizeof(USB_NODE_CONNECTION_INFORMATION_EX_V2),
        };
        v2.SupportedUsbProtocols.Usb300 = true;
        var bytes = MemoryMarshal.AsBytes(new Span<USB_NODE_CONNECTION_INFORMATION_EX_V2>(ref v2));
        return PInvoke.DeviceIoControl(hub, PInvoke.IOCTL_USB_GET_NODE_CONNECTION_INFORMATION_EX_V2, bytes, bytes, out _)
            ? v2.Flags
            : null;
    }
}
