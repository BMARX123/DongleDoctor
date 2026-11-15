using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Devices.DeviceAndDriverInstallation;
using Windows.Win32.Devices.Properties;
using Windows.Win32.Foundation;

namespace DongleDoctor.Platform.Windows.Usb;

/// <summary>Thin wrappers over CfgMgr32 for reading the device tree.</summary>
internal static unsafe class ConfigManager
{
    /// <summary>Instance IDs of present devices under an enumerator such as "USB".</summary>
    public static IReadOnlyList<string> PresentDeviceIds(string enumerator)
    {
        const uint flags = PInvoke.CM_GETIDLIST_FILTER_ENUMERATOR | PInvoke.CM_GETIDLIST_FILTER_PRESENT;
        while (true)
        {
            Check(PInvoke.CM_Get_Device_ID_List_Size(out var length, enumerator, flags), "CM_Get_Device_ID_List_Size");
            var buffer = new char[length];
            fixed (char* p = buffer)
            {
                var cr = PInvoke.CM_Get_Device_ID_List(enumerator, new PZZWSTR(p), length, flags);
                if (cr == CONFIGRET.CR_BUFFER_SMALL)
                {
                    continue; // A device arrived between the two calls.
                }

                Check(cr, "CM_Get_Device_ID_List");
            }

            return SplitMultiString(buffer);
        }
    }

    /// <summary>The devnode handle for an instance ID, or null if the device isn't present.</summary>
    public static uint? Locate(string instanceId)
    {
        fixed (char* p = instanceId)
        {
            var cr = PInvoke.CM_Locate_DevNode(out var devInst, new PWSTR(p), CM_LOCATE_DEVNODE_FLAGS.CM_LOCATE_DEVNODE_NORMAL);
            return cr == CONFIGRET.CR_SUCCESS ? devInst : null;
        }
    }

    public static string? GetString(uint devInst, DEVPROPKEY key)
    {
        var bytes = GetProperty(devInst, key, DEVPROPTYPE.DEVPROP_TYPE_STRING);
        return bytes is null ? null : MemoryMarshal.Cast<byte, char>(bytes).ToString().TrimEnd('\0');
    }

    public static IReadOnlyList<string> GetStringList(uint devInst, DEVPROPKEY key)
    {
        var bytes = GetProperty(devInst, key, DEVPROPTYPE.DEVPROP_TYPE_STRING_LIST);
        return bytes is null ? [] : SplitMultiString(MemoryMarshal.Cast<byte, char>(bytes));
    }

    public static uint? GetUInt32(uint devInst, DEVPROPKEY key)
    {
        var bytes = GetProperty(devInst, key, DEVPROPTYPE.DEVPROP_TYPE_UINT32);
        return bytes is { Length: 4 } ? BitConverter.ToUInt32(bytes) : null;
    }

    public static bool? GetBoolean(uint devInst, DEVPROPKEY key)
    {
        // DEVPROP_BOOLEAN is one byte: 0x00 false, 0xFF true.
        var bytes = GetProperty(devInst, key, DEVPROPTYPE.DEVPROP_TYPE_BOOLEAN);
        return bytes is { Length: 1 } ? bytes[0] != 0 : null;
    }

    /// <summary>The device manager problem code; 0 when the device works.</summary>
    public static uint GetProblemCode(uint devInst)
    {
        var cr = PInvoke.CM_Get_DevNode_Status(out var status, out var problem, devInst, 0);
        return cr == CONFIGRET.CR_SUCCESS && status.HasFlag(CM_DEVNODE_STATUS_FLAGS.DN_HAS_PROBLEM) ? (uint)problem : 0;
    }

    /// <summary>Device interface paths of one class exposed by a device, e.g. a hub's GUID_DEVINTERFACE_USB_HUB path.</summary>
    public static IReadOnlyList<string> InterfacePaths(Guid interfaceClass, string instanceId)
    {
        fixed (char* id = instanceId)
        {
            while (true)
            {
                Check(PInvoke.CM_Get_Device_Interface_List_Size(out var length, interfaceClass, new PWSTR(id),
                    CM_GET_DEVICE_INTERFACE_LIST_FLAGS.CM_GET_DEVICE_INTERFACE_LIST_PRESENT), "CM_Get_Device_Interface_List_Size");
                var buffer = new char[length];
                fixed (char* p = buffer)
                {
                    var cr = PInvoke.CM_Get_Device_Interface_List(interfaceClass, new PWSTR(id), new PZZWSTR(p), length,
                        CM_GET_DEVICE_INTERFACE_LIST_FLAGS.CM_GET_DEVICE_INTERFACE_LIST_PRESENT);
                    if (cr == CONFIGRET.CR_BUFFER_SMALL)
                    {
                        continue;
                    }

                    Check(cr, "CM_Get_Device_Interface_List");
                }

                return SplitMultiString(buffer);
            }
        }
    }

    private static byte[]? GetProperty(uint devInst, DEVPROPKEY key, DEVPROPTYPE expected)
    {
        uint size = 0;
        var cr = PInvoke.CM_Get_DevNode_Property(devInst, key, out _, default, ref size, 0);
        if (cr != CONFIGRET.CR_BUFFER_SMALL)
        {
            return null; // Missing property (CR_NO_SUCH_VALUE) or the device went away.
        }

        var buffer = new byte[size];
        cr = PInvoke.CM_Get_DevNode_Property(devInst, key, out var type, buffer, ref size, 0);
        return cr == CONFIGRET.CR_SUCCESS && type == expected ? buffer[..(int)size] : null;
    }

    internal static IReadOnlyList<string> SplitMultiString(ReadOnlySpan<char> buffer) =>
        buffer.ToString().Split('\0', StringSplitOptions.RemoveEmptyEntries);

    private static void Check(CONFIGRET cr, string function)
    {
        if (cr != CONFIGRET.CR_SUCCESS)
        {
            throw new InvalidOperationException($"{function} failed: {cr}");
        }
    }
}
