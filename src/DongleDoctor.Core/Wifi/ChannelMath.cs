namespace DongleDoctor.Core.Wifi;

/// <summary>Channel numbers, bands and the frequencies a network occupies.</summary>
public static class ChannelMath
{
    public static WifiBand BandOf(int centerMhz) => centerMhz switch
    {
        < 3000 => WifiBand.Ghz24,
        < 5925 => WifiBand.Ghz5,
        _ => WifiBand.Ghz6,
    };

    public static int ChannelOf(int centerMhz) => BandOf(centerMhz) switch
    {
        WifiBand.Ghz24 => centerMhz == 2484 ? 14 : (centerMhz - 2407) / 5,
        WifiBand.Ghz5 => (centerMhz - 5000) / 5,
        _ => (centerMhz - 5950) / 5,
    };

    /// <summary>
    /// Wide channels are fixed blocks of 20 MHz channels: 80 MHz at 5 GHz is 36–48, 52–64 and so on. At 2.4 GHz a
    /// 40 MHz network extends above or below its primary channel, as its beacon says.
    /// </summary>
    internal static (int LowMhz, int HighMhz) Span(WifiNetwork n) => Span(n.Band, n.CenterMhz, n.WidthMhz, !n.SecondaryBelow);

    internal static (int LowMhz, int HighMhz) Span(WifiBand band, int centerMhz, int widthMhz, bool secondaryAbove)
    {
        if (widthMhz <= 20)
        {
            return (centerMhz - 10, centerMhz + 10);
        }

        if (band == WifiBand.Ghz24)
        {
            return secondaryAbove ? (centerMhz - 10, centerMhz + 30) : (centerMhz - 30, centerMhz + 10);
        }

        var low = BlockStart(band, ChannelOf(centerMhz), widthMhz);
        var lowMhz = (band == WifiBand.Ghz5 ? 5000 : 5950) + low * 5 - 10;
        return (lowMhz, lowMhz + widthMhz);
    }

    internal static string Range(WifiNetwork n)
    {
        if (n.WidthMhz <= 20 || n.Band == WifiBand.Ghz24)
        {
            return n.Channel.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        var start = BlockStart(n.Band, n.Channel, n.WidthMhz);
        return $"{start}–{start + (n.WidthMhz / 5) - 4}";
    }

    /// <summary>The first 20 MHz channel of the block a wide channel belongs to.</summary>
    private static int BlockStart(WifiBand band, int channel, int widthMhz)
    {
        // 5 GHz blocks are aligned from channel 36, except the upper band, which starts at 149.
        var origin = band == WifiBand.Ghz6 ? 1 : channel >= 149 ? 149 : 36;
        var step = widthMhz / 5;
        return origin + ((channel - origin) / step * step);
    }
}
