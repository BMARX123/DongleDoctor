using DongleDoctor.Core.Checks;
using DongleDoctor.Core.Wifi;

namespace DongleDoctor.Core.Tests;

public class WifiSurveyTests
{
    private static readonly byte[] FrameBssid = [0x02, 0x11, 0x22, 0x33, 0x44, 0x50];

    // VHT Operation: width 1 (80 MHz), center segment, no second segment.
    private static readonly byte[] Vht80 = [192, 5, 1, 42, 0, 0, 0];

    private static byte[] Load(int percent) => [11, 5, 3, 0, (byte)(percent * 255 / 100), 0, 0];

    private static WifiNetwork Net(int channel, int width, WifiBand band = WifiBand.Ghz5, int rssi = -50)
    {
        var center = band switch { WifiBand.Ghz24 => 2407, WifiBand.Ghz5 => 5000, _ => 5950 } + channel * 5;
        return new WifiNetwork([], band, channel, center, width, rssi, null);
    }

    private static BssEntry Entry(string name, byte[] bssid, int channel, int rssi, params byte[][] ies) =>
        new(name, bssid, 5000 + channel * 5, rssi, [.. ies.SelectMany(i => i)]);

    [Theory]
    [InlineData(36, 80, 5170, 5250)]
    [InlineData(44, 80, 5170, 5250)]
    [InlineData(149, 80, 5735, 5815)]
    [InlineData(100, 160, 5490, 5650)]
    [InlineData(44, 40, 5210, 5250)]
    [InlineData(40, 20, 5190, 5210)]
    public void Span_UsesTheChannelBlock(int channel, int width, int low, int high) =>
        Assert.Equal((low, high), Net(channel, width).Span);

    [Fact]
    public void Overlap_FollowsTheBlocks()
    {
        var frame = Net(36, 80);
        Assert.True(Net(44, 80).Overlaps(frame));
        Assert.True(Net(48, 20).Overlaps(frame));
        Assert.False(Net(52, 20).Overlaps(frame));
        Assert.False(Net(149, 80).Overlaps(frame));
        Assert.False(Net(36, 80, WifiBand.Ghz6).Overlaps(frame));
    }

    [Fact]
    public void TwoPointFour_NeighboursOverlapOnlyWhenClose()
    {
        Assert.False(Net(1, 20, WifiBand.Ghz24).Overlaps(Net(6, 20, WifiBand.Ghz24)));
        Assert.True(Net(1, 20, WifiBand.Ghz24).Overlaps(Net(3, 20, WifiBand.Ghz24)));
    }

    [Fact]
    public void ChannelRange_NamesTheBlock()
    {
        Assert.Equal("36–48", Net(44, 80).ChannelRange);
        Assert.Equal("100–128", Net(112, 160).ChannelRange);
        Assert.Equal("40", Net(40, 20).ChannelRange);
    }

    [Fact]
    public void Beacon_ReadsWidthAndLoad()
    {
        Assert.Equal(new BeaconInfo(80, false, 32), BeaconInfo.Parse([.. Vht80, .. Load(32)]));

        // HT Operation: secondary channel below, 40 MHz allowed.
        Assert.Equal(new BeaconInfo(40, true, null), BeaconInfo.Parse([61, 22, 6, 0b111, .. new byte[20]]));

        // EHT Operation: information present, control byte says 160 MHz.
        Assert.Equal(160, BeaconInfo.Parse([255, 9, 106, 1, 0, 0, 0, 0, 3, 50, 0]).WidthMhz);

        // HE Operation with 6 GHz information: 80 MHz.
        Assert.Equal(80, BeaconInfo.Parse([255, 12, 36, 0, 0, 0b10, 0, 0, 0, 5, 2, 7, 0, 0]).WidthMhz);
    }

    [Fact]
    public void Beacon_TruncatedElements_AreIgnored() =>
        Assert.Equal(new BeaconInfo(20, false, null), BeaconInfo.Parse([192, 9, 1, 42]));

    [Fact]
    public void Analyze_FindsTheFrame_GroupsRadios_AndFlagsStrongOverlaps()
    {
        var sweep = new WifiSweep(
            new WifiLink(FrameBssid, 100, 1201, 1201),
            [
                Entry("", FrameBssid, 36, -46, Vht80),
                Entry("Neighbour", [0x00, 0xAA, 0xBB, 0xCC, 0xDD, 0x10], 44, -55, Vht80, Load(32)),
                Entry("Neighbour-Guest", [0x02, 0xAA, 0xBB, 0xCC, 0xDD, 0x11], 44, -56, Vht80, Load(32)),
                Entry("Far", [0x00, 0x01, 0x02, 0x03, 0x04, 0x05], 44, -77, Vht80),
                Entry("Home", [0x00, 0x09, 0x09, 0x09, 0x09, 0x09], 149, -32, Vht80),
            ]);

        var survey = WifiAnalysis.Analyze(sweep);

        Assert.Equal(36, survey.Frame!.Channel);
        Assert.Equal(3, survey.Networks.Count);
        var conflict = Assert.Single(survey.Conflicts);
        Assert.Equal(["Neighbour", "Neighbour-Guest"], conflict.Names);
        Assert.Equal(-55, conflict.Rssi);
        Assert.Equal(32, conflict.LoadPercent);
        Assert.Equal(-77, Assert.Single(survey.WeakOverlaps).Rssi);

        var finding = WifiAnalysis.ToFinding(survey);
        Assert.Equal(Severity.Warning, finding.Severity);
        Assert.DoesNotContain("Neighbour", finding.Details);
    }

    [Fact]
    public void Analyze_WithoutTheFrame_SaysSo()
    {
        var survey = WifiAnalysis.Analyze(new WifiSweep(null, [Entry("Home", [0, 9, 9, 9, 9, 9], 149, -32, Vht80)]));
        Assert.Null(survey.Frame);
        Assert.Empty(survey.Conflicts);
        Assert.Equal(Severity.Info, WifiAnalysis.ToFinding(survey).Severity);
    }

    [Theory]
    [InlineData(WifiBand.Ghz5, Severity.Warning)]
    [InlineData(WifiBand.Ghz24, Severity.Warning)]
    [InlineData(WifiBand.Ghz6, Severity.Pass)]
    public void Band_WarnsUnlessSixGhz(WifiBand band, Severity expected)
    {
        var finding = WifiAnalysis.BandFinding(Net(37, 160, band));
        Assert.Equal(WifiAnalysis.BandCheckId, finding.CheckId);
        Assert.Equal(expected, finding.Severity);
    }

    [Fact]
    public void Findings_AreBandThenChannel_OrOnlyChannelWithoutTheFrame()
    {
        var withFrame = WifiAnalysis.Analyze(new WifiSweep(
            new WifiLink(FrameBssid, 100, 1201, 1201), [Entry("", FrameBssid, 36, -46, Vht80)]));
        Assert.Equal([WifiAnalysis.BandCheckId, WifiAnalysis.CheckId], WifiAnalysis.ToFindings(withFrame).Select(f => f.CheckId));

        var without = WifiAnalysis.Analyze(new WifiSweep(null, []));
        Assert.Equal([WifiAnalysis.CheckId], WifiAnalysis.ToFindings(without).Select(f => f.CheckId));
    }

    [Fact]
    public void ClearChannel_Passes()
    {
        var survey = WifiAnalysis.Analyze(new WifiSweep(
            new WifiLink(FrameBssid, 100, 1201, 1201),
            [Entry("", FrameBssid, 36, -46, Vht80), Entry("Home", [0, 9, 9, 9, 9, 9], 149, -32, Vht80)]));
        Assert.Equal(Severity.Pass, WifiAnalysis.ToFinding(survey).Severity);
    }
}
