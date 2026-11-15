using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using DongleDoctor.Core.Wifi;
using DongleDoctor.Platform.Windows.Adapter;

namespace DongleDoctor.Platform.Windows.Wifi;

/// <summary>Sweeps with the dongle through the Native Wi-Fi API (wlanapi), which needs no admin rights.</summary>
public sealed partial class WindowsWifiSurveyor : IWifiSurveyor
{
    /// <summary>
    /// A stream measured 49 Mbps in a game (automatic bitrate); an idle link to the Frame carries almost nothing.
    /// 4 Mbps leaves room for quieter scenes.
    /// </summary>
    public const long StreamingBytesPerSecond = 500_000;

    // Windows requires drivers to finish a scan within 4 seconds.
    private static readonly TimeSpan ScanTime = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan TrafficSample = TimeSpan.FromSeconds(1);

    public async Task<bool> IsStreamingAsync(CancellationToken cancellationToken = default)
    {
        if (FindAdapter() is not { } adapter)
        {
            return false;
        }

        var before = Bytes(adapter);
        await Task.Delay(TrafficSample, cancellationToken).ConfigureAwait(false);
        return (Bytes(adapter) - before) / TrafficSample.TotalSeconds >= StreamingBytesPerSecond;
    }

    public async Task<WifiSweep> SweepAsync(CancellationToken cancellationToken = default)
    {
        var iface = InterfaceId() ?? throw new InvalidOperationException("The dongle isn't plugged in");
        using (var client = WlanClient.Open())
        {
            Check(WlanScan(client.Handle, in iface, 0, 0, 0), "WlanScan");
        }

        await Task.Delay(ScanTime, cancellationToken).ConfigureAwait(false);
        using var reader = WlanClient.Open();
        return new WifiSweep(ReadLink(reader.Handle, iface), ReadBssList(reader.Handle, iface));
    }

    /// <summary>The dongle's link to the Frame, without sweeping. Null if it isn't connected.</summary>
    public static WifiLink? ReadLink()
    {
        if (InterfaceId() is not { } iface)
        {
            return null;
        }

        using var client = WlanClient.Open();
        return ReadLink(client.Handle, iface);
    }

    private static NetworkInterface? FindAdapter() =>
        InterfaceId() is { } id
            ? NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(n => Guid.TryParse(n.Id, out var g) && g == id)
            : null;

    private static long Bytes(NetworkInterface adapter)
    {
        var s = adapter.GetIPStatistics();
        return s.BytesReceived + s.BytesSent;
    }

    private static Guid? InterfaceId()
    {
        using var key = DongleDriverKey.Open(out _);
        return key?.GetValue("NetCfgInstanceId") is string id && Guid.TryParse(id, out var guid) ? guid : null;
    }

    // WLAN_BSS_ENTRY on x64: SSID 0–36, BSSID 40, lRssi 56, ulChCenterFrequency 92 (kHz), ulIeOffset 352, ulIeSize 356.
    private const int BssEntrySize = 360;

    private static List<BssEntry> ReadBssList(nint client, Guid iface)
    {
        Check(WlanGetNetworkBssList(client, in iface, 0, BssTypeAny, false, 0, out var list), "WlanGetNetworkBssList");
        try
        {
            var entries = new List<BssEntry>();
            var count = Marshal.ReadInt32(list, 4);
            for (var i = 0; i < count; i++)
            {
                var e = list + 8 + (i * BssEntrySize);
                var ieOffset = Marshal.ReadInt32(e, 352);
                var ies = new byte[Marshal.ReadInt32(e, 356)];
                Marshal.Copy(e + ieOffset, ies, 0, ies.Length);
                entries.Add(new BssEntry(Ssid(e), Copy(e + 40, 6), Marshal.ReadInt32(e, 92) / 1000, Marshal.ReadInt32(e, 56), ies));
            }

            return entries;
        }
        finally
        {
            WlanFreeMemory(list);
        }
    }

    // WLAN_CONNECTION_ATTRIBUTES: isState 0, profile name 8–520, then association attributes: BSSID 560,
    // signal quality 576, rx rate 580 and tx rate 584 (kbps).
    private static WifiLink? ReadLink(nint client, Guid iface)
    {
        if (WlanQueryInterface(client, in iface, CurrentConnection, 0, out _, out var data, out _) != 0)
        {
            return null; // Not connected.
        }

        try
        {
            return Marshal.ReadInt32(data) != StateConnected
                ? null
                : new WifiLink(Copy(data + 560, 6), Marshal.ReadInt32(data, 576),
                    Marshal.ReadInt32(data, 580) / 1000, Marshal.ReadInt32(data, 584) / 1000);
        }
        finally
        {
            WlanFreeMemory(data);
        }
    }

    private static string Ssid(nint entry)
    {
        var length = Math.Clamp(Marshal.ReadInt32(entry), 0, 32);
        return Encoding.UTF8.GetString(Copy(entry + 4, length));
    }

    private static byte[] Copy(nint source, int length)
    {
        var bytes = new byte[length];
        Marshal.Copy(source, bytes, 0, length);
        return bytes;
    }

    private static void Check(uint error, string call)
    {
        if (error != 0)
        {
            throw new InvalidOperationException($"{call} failed with error {error}");
        }
    }

    private const int BssTypeAny = 3;
    private const int CurrentConnection = 7;
    private const int StateConnected = 1;

    private sealed class WlanClient : IDisposable
    {
        private WlanClient(nint handle) => Handle = handle;

        public nint Handle { get; }

        public static WlanClient Open()
        {
            Check(WlanOpenHandle(2, 0, out _, out var handle), "WlanOpenHandle");
            return new WlanClient(handle);
        }

        public void Dispose() => WlanCloseHandle(Handle, 0);
    }

    [LibraryImport("wlanapi.dll")]
    private static partial uint WlanOpenHandle(uint clientVersion, nint reserved, out uint negotiatedVersion, out nint handle);

    [LibraryImport("wlanapi.dll")]
    private static partial uint WlanCloseHandle(nint handle, nint reserved);

    [LibraryImport("wlanapi.dll")]
    private static partial uint WlanScan(nint handle, in Guid iface, nint ssid, nint ieData, nint reserved);

    [LibraryImport("wlanapi.dll")]
    private static partial uint WlanGetNetworkBssList(
        nint handle, in Guid iface, nint ssid, int bssType, [MarshalAs(UnmanagedType.Bool)] bool securityEnabled, nint reserved, out nint list);

    [LibraryImport("wlanapi.dll")]
    private static partial uint WlanQueryInterface(
        nint handle, in Guid iface, int opCode, nint reserved, out uint dataSize, out nint data, out int valueType);

    [LibraryImport("wlanapi.dll")]
    private static partial void WlanFreeMemory(nint memory);
}
