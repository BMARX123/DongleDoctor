namespace DongleDoctor.Core.Wifi;

public enum WifiBand
{
    Ghz24,
    Ghz5,
    Ghz6,
}

/// <summary>One access point radio the dongle heard, possibly advertising several network names.</summary>
/// <param name="Names">The network names it advertises. Shown on screen to help the user spot their own router; never logged.</param>
/// <param name="CenterMhz">The primary 20 MHz channel's center frequency.</param>
/// <param name="LoadPercent">How busy the access point says the channel is, if it says.</param>
/// <param name="SecondaryBelow">For a 40 MHz network at 2.4 GHz: it extends below its primary channel rather than above.</param>
public sealed record WifiNetwork(
    IReadOnlyList<string> Names,
    WifiBand Band,
    int Channel,
    int CenterMhz,
    int WidthMhz,
    int Rssi,
    int? LoadPercent,
    bool SecondaryBelow = false)
{
    /// <summary>The frequencies the network occupies, from its primary channel and width.</summary>
    public (int LowMhz, int HighMhz) Span => ChannelMath.Span(this);

    public bool Overlaps(WifiNetwork other) => Band == other.Band && Span.LowMhz < other.Span.HighMhz && other.Span.LowMhz < Span.HighMhz;

    /// <summary>"36" for 20 MHz, otherwise the channels the block covers, such as "36–48".</summary>
    public string ChannelRange => ChannelMath.Range(this);
}

/// <summary>A raw entry from the Wi-Fi driver's list of networks, before grouping.</summary>
/// <param name="Bssid">The access point's address. Used only to group radios and find the Frame; never logged.</param>
/// <param name="InformationElements">The beacon's information elements, which carry width and load.</param>
public sealed record BssEntry(string Name, byte[] Bssid, int CenterMhz, int Rssi, byte[] InformationElements);

/// <summary>What the dongle reports about its connection to the Frame.</summary>
public sealed record WifiLink(byte[] Bssid, int SignalPercent, int RxMbps, int TxMbps);

/// <summary>A sweep's raw result.</summary>
public sealed record WifiSweep(WifiLink? Link, IReadOnlyList<BssEntry> Entries);
