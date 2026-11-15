using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DongleDoctor.Core.Checks;
using DongleDoctor.Core.Wifi;

namespace DongleDoctor.App.ViewModels;

public enum WifiStep
{
    Checking,
    StopStreaming,
    Sweeping,
    Results,
}

/// <summary>
/// The "Wi-Fi around your Frame" screen. A sweep takes the dongle off the Frame's channel, so it only runs once the
/// Frame isn't streaming: until then the user is asked to stop, and asked again if they're still streaming.
/// </summary>
public partial class WifiViewModel(
    IWifiSurveyor surveyor, Action<WifiSurvey?, IReadOnlyList<Finding>> finished, Action<string> openGuide) : ViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy), nameof(IsStopStreaming), nameof(IsResults), nameof(BusyText))]
    public partial WifiStep Step { get; set; }

    public bool IsBusy => Step is WifiStep.Checking or WifiStep.Sweeping;

    public bool IsStopStreaming => Step == WifiStep.StopStreaming;

    public bool IsResults => Step == WifiStep.Results;

    public string BusyText => Step == WifiStep.Sweeping ? "Looking at the Wi-Fi around you…" : "Checking your Frame…";

    /// <summary>Shown when the user said they'd stopped but the Frame was still streaming.</summary>
    [ObservableProperty]
    public partial bool StillStreaming { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Headline))]
    public partial IReadOnlyList<Finding> Findings { get; set; } = [];

    /// <summary>The findings as rows, each with its severity stripe and guide button.</summary>
    public IReadOnlyList<FindingRowViewModel> FindingRows => [.. Findings.Select(f => new FindingRowViewModel(f, openGuide))];

    public string Headline => Findings.Any(f => f.Severity >= Severity.Warning)
        ? "Your Frame's Wi-Fi could be better"
        : Findings.Any(f => f.Severity == Severity.Info)
            ? Findings.First(f => f.Severity == Severity.Info).Title
            : "The Wi-Fi around your Frame looks good";

    partial void OnFindingsChanged(IReadOnlyList<Finding> value) => OnPropertyChanged(nameof(FindingRows));

    [ObservableProperty]
    public partial string LinkSummary { get; set; } = "";

    [ObservableProperty]
    public partial IReadOnlyList<WifiNetworkRow> Conflicts { get; set; } = [];

    [ObservableProperty]
    public partial string OthersSummary { get; set; } = "";

    [ObservableProperty]
    public partial WifiChart? Chart { get; set; }

    public bool HasConflicts => Conflicts.Count > 0;

    private WifiSurvey? _survey;

    public async Task RunAsync()
    {
        Step = WifiStep.Checking;
        if (await surveyor.IsStreamingAsync())
        {
            Step = WifiStep.StopStreaming;
            return;
        }

        Step = WifiStep.Sweeping;
        try
        {
            _survey = WifiAnalysis.Analyze(await surveyor.SweepAsync());
            Findings = WifiAnalysis.ToFindings(_survey);
        }
        catch (Exception e)
        {
            _survey = null;
            Findings =
            [
                new Finding(WifiAnalysis.CheckId, Severity.Info, "Couldn't check the Wi-Fi around you",
                    "The dongle didn't answer. You can still scan while you play.", Details: e.Message),
            ];
        }

        Show(_survey);
        Step = WifiStep.Results;
    }

    [RelayCommand]
    private async Task StoppedStreaming()
    {
        StillStreaming = false;
        await RunAsync();
        StillStreaming = Step == WifiStep.StopStreaming;
    }

    [RelayCommand]
    private Task SweepAgain() => RunAsync();

    [RelayCommand]
    private void Continue() => finished(_survey, Findings);

    private void Show(WifiSurvey? survey)
    {
        if (survey?.Frame is not { } frame)
        {
            LinkSummary = "";
            Conflicts = [];
            OthersSummary = "";
            Chart = null;
            OnPropertyChanged(nameof(HasConflicts));
            return;
        }

        var link = survey.Link is { } l ? $" · {l.SignalPercent}% signal · {l.RxMbps} Mbps" : "";
        LinkSummary = $"Your Frame: channel {frame.ChannelRange}, {WifiAnalysis.BandName(frame.Band)}, {frame.WidthMhz} MHz wide{link}";
        Conflicts = [.. survey.Conflicts.Select(n => new WifiNetworkRow(n))];
        OnPropertyChanged(nameof(HasConflicts));

        var weak = survey.WeakOverlaps.Count;
        var elsewhere = survey.Networks.Count - survey.Conflicts.Count - weak;
        OthersSummary = (weak > 0 ? $"{weak} weaker {Plural(weak, "network")} also overlap{(weak == 1 ? "s" : "")}. " : "")
            + $"{elsewhere} other {Plural(elsewhere, "network")} nearby use other channels.";
        Chart = WifiChart.For(frame, survey.Networks, survey.Conflicts);
    }

    private static string Plural(int n, string noun) => n == 1 ? noun : noun + "s";
}

/// <summary>One conflicting network in the list.</summary>
public sealed class WifiNetworkRow(WifiNetwork network)
{
    public string Name => network.Names.Count switch
    {
        0 => "Hidden network",
        1 => network.Names[0],
        _ => $"{network.Names[0]} (+{network.Names.Count - 1} more)",
    };

    public string Detail
    {
        get
        {
            var load = network.LoadPercent is { } p ? $" · {p}% busy" : "";
            return $"Channel {network.ChannelRange} · {WifiAnalysis.Strength(network.Rssi)} signal{load}";
        }
    }
}

/// <summary>What the channel chart draws: the Frame's band, with one bar per network.</summary>
public sealed record WifiChart(int LowMhz, int HighMhz, IReadOnlyList<WifiChart.Bar> Bars, IReadOnlyList<(int Mhz, string Label)> Ticks)
{
    public enum Kind
    {
        Other,
        Conflict,
        Frame,
    }

    public sealed record Bar(int LowMhz, int HighMhz, int Rssi, Kind Kind);

    public static WifiChart For(WifiNetwork frame, IReadOnlyList<WifiNetwork> networks, IReadOnlyList<WifiNetwork> conflicts)
    {
        var bars = networks.Where(n => n.Band == frame.Band)
            .Select(n => new Bar(n.Span.LowMhz, n.Span.HighMhz, n.Rssi, conflicts.Contains(n) ? Kind.Conflict : Kind.Other))
            .OrderBy(b => b.Kind)
            .Append(new Bar(frame.Span.LowMhz, frame.Span.HighMhz, frame.Rssi, Kind.Frame))
            .ToList();
        return frame.Band switch
        {
            WifiBand.Ghz24 => new(2400, 2495, bars, TicksAt(WifiBand.Ghz24, 1, 6, 11)),
            WifiBand.Ghz5 => new(5150, 5895, bars, TicksAt(WifiBand.Ghz5, 36, 52, 100, 116, 132, 149, 165)),
            _ => new(5925, 7125, bars, TicksAt(WifiBand.Ghz6, 1, 33, 65, 97, 129, 161, 193, 225)),
        };
    }

    private static List<(int, string)> TicksAt(WifiBand band, params int[] channels) =>
        [.. channels.Select(c => ((band switch { WifiBand.Ghz24 => 2407, WifiBand.Ghz5 => 5000, _ => 5950 }) + (c * 5), c.ToString(System.Globalization.CultureInfo.InvariantCulture)))];
}
