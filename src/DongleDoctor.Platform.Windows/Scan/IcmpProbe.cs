using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using DongleDoctor.Core.Scan;
using DongleDoctor.Platform.Windows.Adapter;

namespace DongleDoctor.Platform.Windows.Scan;

/// <summary>
/// Pings the Frame through the dongle and records each round trip, timed by the scan clock rather than taken from
/// the reply: some fast replies report TimedOut, and a lost ping should count as a long one.
/// </summary>
public sealed class IcmpProbe(Func<IPAddress?>? findTarget = null) : IProbe
{
    // Aim for 50 Hz. Windows timers round this up, so the real rate is lower; one stalled ping is enough to see a pause.
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(20);
    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(1);

    public string Name => "Ping to the Frame";

    public async Task RunAsync(ScanClock clock, Action<ScanEvent> emit, CancellationToken cancellationToken)
    {
        // The headset may connect after the scan starts.
        var find = findTarget ?? FindFrameAddress;
        IPAddress? target;
        while ((target = find()) is null)
        {
            await Task.Delay(RetryInterval, clock.Time, cancellationToken).ConfigureAwait(false);
        }

        using var ping = new Ping();
        var payload = new byte[32];
        var next = clock.Now;
        while (!cancellationToken.IsCancellationRequested)
        {
            var sent = clock.Now;
            try
            {
                await ping.SendPingAsync(target, Timeout, payload, null, cancellationToken).ConfigureAwait(false);
            }
            catch (PingException)
            {
                // No route right now (e.g. the headset went to sleep); counts like a lost ping.
            }

            emit(new ScanEvent(sent, ScanEventKind.Ping, (clock.Now - sent).TotalMilliseconds));
            next += Interval;
            var wait = next - clock.Now;
            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, clock.Time, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                next = clock.Now;
            }
        }
    }

    /// <summary>
    /// The Frame's address: the dongle's default gateway, since the Frame is the access point the dongle joins.
    /// Null if the dongle isn't plugged in or isn't connected to the Frame.
    /// </summary>
    public static IPAddress? FindFrameAddress()
    {
        using var key = DongleDriverKey.Open(out _);
        if (key?.GetValue("NetCfgInstanceId") is not string interfaceId)
        {
            return null;
        }

        var adapter = NetworkInterface.GetAllNetworkInterfaces()
            .FirstOrDefault(n => n.Id.Equals(interfaceId, StringComparison.OrdinalIgnoreCase));
        if (adapter is not { OperationalStatus: OperationalStatus.Up })
        {
            return null;
        }

        var ip = adapter.GetIPProperties();
        return ip.GatewayAddresses.Select(g => g.Address).Concat(ip.DhcpServerAddresses)
            .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !a.Equals(IPAddress.Any));
    }
}
