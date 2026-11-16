using System.Globalization;
using DongleDoctor.Core.Checks;

namespace DongleDoctor.Core.Wifi;

/// <summary>What a beacon says about its channel.</summary>
public sealed record BeaconInfo(int WidthMhz, bool SecondaryBelow, int? LoadPercent)
{
    /// <summary>Reads channel width and load from a beacon's information elements, taking the widest width advertised.</summary>
    public static BeaconInfo Parse(ReadOnlySpan<byte> ies)
    {
        int width = 20;
        bool below = false;
        int? load = null;
        for (var p = 0; p + 2 <= ies.Length;)
        {
            int id = ies[p], len = ies[p + 1];
            if (p + 2 + len > ies.Length)
            {
                break;
            }

            var d = ies.Slice(p + 2, len);
            switch (id)
            {
                case 11 when len >= 3:
                    // BSS Load: station count (2 bytes), then channel utilization out of 255.
                    load = (int)Math.Round(d[2] * 100 / 255.0);
                    break;
                case 61 when len >= 2 && (d[1] & 0b100) != 0 && (d[1] & 0b11) is 1 or 3:
                    // HT Operation: a secondary channel above (1) or below (3), with 40 MHz allowed.
                    width = Math.Max(width, 40);
                    below = (d[1] & 0b11) == 3;
                    break;
                case 192 when len >= 3:
                    width = Math.Max(width, VhtWidth(d[0], d[1], d[2]));
                    break;
                case 255 when len >= 1:
                    width = Math.Max(width, ExtensionWidth(d));
                    break;
            }

            p += 2 + len;
        }

        return new BeaconInfo(width, below, load);
    }

    private static int VhtWidth(byte channelWidth, byte ccfs0, byte ccfs1) => channelWidth switch
    {
        // Width 1 with a second center segment is how 160 and 80+80 MHz are signaled.
        1 when ccfs1 != 0 => 160,
        1 => 80,
        2 or 3 => 160,
        _ => 20,
    };

    private static int ExtensionWidth(ReadOnlySpan<byte> d)
    {
        switch (d[0])
        {
            case 36 when d.Length >= 7:
            {
                // HE Operation: parameters (3), BSS color (1), basic MCS (2), then optional parts. The 6 GHz part
                // carries the channel width.
                var parameters = d[1] | (d[2] << 8) | (d[3] << 16);
                var offset = 7 + ((parameters & (1 << 14)) != 0 ? 3 : 0) + ((parameters & (1 << 15)) != 0 ? 1 : 0);
                return (parameters & (1 << 17)) != 0 && d.Length >= offset + 2
                    ? (d[offset + 1] & 0b11) switch { 1 => 40, 2 => 80, 3 => 160, _ => 20 }
                    : 20;
            }

            case 106 when d.Length >= 7 && (d[1] & 1) != 0:
                // EHT Operation: parameters (1), basic MCS (4), then the operation information's control byte.
                return (d[6] & 0b111) switch { 1 => 40, 2 => 80, 3 => 160, 4 => 320, _ => 20 };
            default:
                return 20;
        }
    }
}

/// <summary>The Wi-Fi around the Frame, and which networks share its channel.</summary>
/// <param name="Frame">The Frame's network, or null if the dongle isn't connected to it.</param>
/// <param name="Networks">Every other radio heard, strongest first.</param>
/// <param name="Conflicts">Networks overlapping the Frame's channel at <see cref="WifiAnalysis.ConflictRssi"/> or stronger.</param>
/// <param name="WeakOverlaps">Networks overlapping the Frame's channel, but too weak to matter much.</param>
public sealed record WifiSurvey(
    WifiNetwork? Frame,
    WifiLink? Link,
    IReadOnlyList<WifiNetwork> Networks,
    IReadOnlyList<WifiNetwork> Conflicts,
    IReadOnlyList<WifiNetwork> WeakOverlaps);

public static class WifiAnalysis
{
    public const string CheckId = "wifi.channel";

    public const string BandCheckId = "wifi.band";

    /// <summary>An overlapping network this strong or stronger is a conflict. About 60% signal; provisional.</summary>
    public const int ConflictRssi = -70;

    public static WifiSurvey Analyze(WifiSweep sweep)
    {
        var radios = sweep.Entries
            .GroupBy(e => RadioKey(e))
            .Select(g => (Bssids: g.Select(e => e.Bssid).ToList(), Network: Merge(g.ToList())))
            .ToList();

        var frameRadio = sweep.Link is { } link
            ? radios.FirstOrDefault(r => r.Bssids.Any(b => b.AsSpan().SequenceEqual(link.Bssid)))
            : default;
        var frame = frameRadio.Network;
        var others = radios.Where(r => !ReferenceEquals(r.Network, frame)).Select(r => r.Network)
            .OrderByDescending(n => n.Rssi).ToList();
        var overlapping = frame is null ? [] : others.Where(n => n.Overlaps(frame)).ToList();
        return new WifiSurvey(
            frame,
            sweep.Link,
            others,
            [.. overlapping.Where(n => n.Rssi >= ConflictRssi)],
            [.. overlapping.Where(n => n.Rssi < ConflictRssi)]);
    }

    /// <summary>The band check, then the channel check. Without the Frame there's only the channel check, saying so.</summary>
    public static IReadOnlyList<Finding> ToFindings(WifiSurvey survey) =>
        survey.Frame is { } frame ? [BandFinding(frame), ToFinding(survey)] : [ToFinding(survey)];

    /// <summary>
    /// The dongle supports 6 GHz, the clearest band, but the Frame picks the band. The Frame's own Wi-Fi chip also
    /// connects it to the home network, and when that connection is on 6 GHz, the Frame puts the dongle's link on
    /// 5 GHz instead.
    /// </summary>
    public static Finding BandFinding(WifiNetwork frame)
    {
        var details = string.Create(CultureInfo.InvariantCulture,
            $"Frame link: {BandName(frame.Band)} ch {frame.Channel}, {frame.WidthMhz} MHz, {frame.Rssi} dBm");
        return frame.Band == WifiBand.Ghz6
            ? new Finding(BandCheckId, Severity.Pass, "Your game stream is on 6 GHz",
                "6 GHz is the clearest band for streaming.", Details: details)
            : new Finding(BandCheckId, Severity.Warning, $"Your game stream is on {BandName(frame.Band)}, not 6 GHz",
                "This usually happens when your Frame is connected to your home Wi-Fi on 6 GHz. Connect your Frame to your router's 5 GHz network instead, and the stream can move to 6 GHz.",
                FindingAction.Guide, details);
    }

    public static Finding ToFinding(WifiSurvey survey)
    {
        if (survey.Frame is not { } frame)
        {
            return new Finding(CheckId, Severity.Info, "Couldn't find your Frame's Wi-Fi",
                "Turn on your Frame so the dongle connects, then check again.");
        }

        var where = $"channel {frame.ChannelRange} ({BandName(frame.Band)})";
        var details = string.Create(CultureInfo.InvariantCulture,
            $"Frame {where}, {frame.WidthMhz} MHz, {frame.Rssi} dBm. Overlapping: {Describe(survey.Conflicts)}; weak: {Describe(survey.WeakOverlaps)}");
        if (survey.Conflicts.Count == 0)
        {
            return new Finding(CheckId, Severity.Pass, "No strong Wi-Fi on your Frame's channel",
                "Nothing nearby takes airtime from your Frame.", Details: details);
        }

        var n = survey.Conflicts.Count;
        return new Finding(CheckId, Severity.Warning,
            n == 1 ? "A Wi-Fi network shares your Frame's channel" : $"{n} Wi-Fi networks share your Frame's channel",
            "They take airtime from your Frame. If one is your router, move it to another channel.",
            FindingAction.Guide, details);
    }

    public static string BandName(WifiBand band) => band switch
    {
        WifiBand.Ghz24 => "2.4 GHz",
        WifiBand.Ghz5 => "5 GHz",
        _ => "6 GHz",
    };

    /// <summary>Plain words for signal strength, since the UI doesn't show dBm.</summary>
    public static string Strength(int rssi) => rssi switch
    {
        >= -60 => "strong",
        >= ConflictRssi => "medium",
        _ => "weak",
    };

    // One radio advertises several networks from addresses that differ only in the last few bits and the
    // "locally administered" bit, all on the same channel.
    private static (int, int, int, int, int, int) RadioKey(BssEntry e) =>
        e.Bssid.Length < 6
            ? (e.CenterMhz, 0, 0, 0, 0, e.GetHashCode())
            : (e.CenterMhz, e.Bssid[1], e.Bssid[2], e.Bssid[3], e.Bssid[4], e.Bssid[5] & 0xF0);

    private static WifiNetwork Merge(List<BssEntry> entries)
    {
        var infos = entries.Select(e => BeaconInfo.Parse(e.InformationElements)).ToList();
        var center = entries[0].CenterMhz;
        var widest = infos.MaxBy(i => i.WidthMhz)!;
        return new WifiNetwork(
            [.. entries.Select(e => e.Name).Where(s => s.Length > 0).Distinct()],
            ChannelMath.BandOf(center),
            ChannelMath.ChannelOf(center),
            center,
            widest.WidthMhz,
            entries.Max(e => e.Rssi),
            infos.Max(i => i.LoadPercent),
            widest.SecondaryBelow);
    }

    private static string Describe(IReadOnlyList<WifiNetwork> networks) =>
        networks.Count == 0
            ? "none"
            : string.Join(", ", networks.Select(n => string.Create(CultureInfo.InvariantCulture, $"ch {n.Channel}/{n.WidthMhz} {n.Rssi} dBm")));
}
