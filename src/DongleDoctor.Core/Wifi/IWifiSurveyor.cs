namespace DongleDoctor.Core.Wifi;

/// <summary>Sweeps for nearby Wi-Fi with the dongle. A sweep can take the radio off the Frame's channel, so only sweep while not streaming.</summary>
public interface IWifiSurveyor
{
    /// <summary>Whether the dongle is carrying a stream right now, judged from its traffic.</summary>
    Task<bool> IsStreamingAsync(CancellationToken cancellationToken = default);

    /// <summary>Asks the dongle to sweep every channel, then returns what it heard and its link to the Frame.</summary>
    Task<WifiSweep> SweepAsync(CancellationToken cancellationToken = default);
}
