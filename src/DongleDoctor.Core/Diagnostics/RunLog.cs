using System.Globalization;
using System.Text;
using DongleDoctor.Core.Checks;
using DongleDoctor.Core.Scan;
using DongleDoctor.Core.Wifi;

namespace DongleDoctor.Core.Diagnostics;

/// <summary>
/// A plain-text log of each run, for working out what happened afterwards. Each run of the checks starts a new file,
/// and the scans that follow are added to it. Only the newest files are kept. Logging never interrupts the app.
/// </summary>
public sealed class RunLog(string directory, int keep = 20)
{
    private string? _path;

    public static string DefaultDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DongleDoctor", "logs");

    /// <summary>Starts a new log file with the instant checks' findings.</summary>
    public void Checks(IReadOnlyList<Finding> findings, DateTime now)
    {
        _path = Path.Combine(directory, $"run-{now:yyyyMMdd-HHmmss}.txt");
        Write(FormatChecks(findings, now), prune: true);
    }

    /// <summary>Adds a finished scan to the current log file.</summary>
    /// <param name="chimePlayed">Whether the end-of-scan sound started; null when none was due (stopped early).</param>
    public void Scan(DateTime started, TimeSpan requested, bool stoppedEarly, ScanReport report, bool? chimePlayed) =>
        Write(FormatScan(started, requested, stoppedEarly, report, chimePlayed), prune: false);

    /// <summary>Adds a Wi-Fi sweep to the current log file. Network names and addresses are left out.</summary>
    public void Wifi(WifiSurvey? survey, IReadOnlyList<Finding> findings) => Write(FormatWifi(survey, findings), prune: false);

    internal static string FormatWifi(WifiSurvey? survey, IReadOnlyList<Finding> findings)
    {
        var s = new StringBuilder();
        s.AppendLine();
        s.AppendLine("Wi-Fi");
        foreach (var finding in findings)
        {
            s.AppendLine(Invariant($"  {finding.Severity,-8} {finding.CheckId}: {finding.Title}"));
        }

        if (survey?.Frame is { } f)
        {
            s.AppendLine(Invariant($"  Frame:   {WifiAnalysis.BandName(f.Band)} ch {f.ChannelRange}, {f.WidthMhz} MHz, {f.Rssi} dBm"));
        }

        if (survey?.Link is { } l)
        {
            s.AppendLine(Invariant($"  Link:    {l.SignalPercent}%, rx {l.RxMbps} Mbps, tx {l.TxMbps} Mbps"));
        }

        if (survey is not null)
        {
            var bands = survey.Networks.GroupBy(n => n.Band).OrderBy(g => g.Key)
                .Select(g => Invariant($"{WifiAnalysis.BandName(g.Key)} {g.Count()}"));
            s.AppendLine(Invariant($"  Radios:  {survey.Networks.Count} ({string.Join(", ", bands)})"));
            foreach (var n in survey.Conflicts)
            {
                s.AppendLine(Invariant($"  Overlap: ch {n.Channel}/{n.WidthMhz} MHz, {n.Rssi} dBm, load {(n.LoadPercent is { } p ? $"{p}%" : "-")}"));
            }

            s.AppendLine(Invariant($"  Weak overlaps: {survey.WeakOverlaps.Count}"));
        }
        else
        {
            foreach (var details in findings.Select(f => f.Details).OfType<string>())
            {
                s.AppendLine(Invariant($"  {details}"));
            }
        }

        return s.ToString();
    }

    internal static string FormatChecks(IReadOnlyList<Finding> findings, DateTime now)
    {
        var s = new StringBuilder();
        s.AppendLine(Invariant($"DongleDoctor {typeof(RunLog).Assembly.GetName().Version} · {now:yyyy-MM-dd HH:mm:ss}"));
        s.AppendLine();
        s.AppendLine("Checks");
        foreach (var f in findings)
        {
            s.AppendLine(Invariant($"  {f.Severity,-8} {f.CheckId}: {f.Title}"));
            if (f.Details is not null)
            {
                s.AppendLine(Invariant($"           {f.Details}"));
            }
        }

        return s.ToString();
    }

    internal static string FormatScan(DateTime started, TimeSpan requested, bool stoppedEarly, ScanReport report, bool? chimePlayed)
    {
        var s = new StringBuilder();
        s.AppendLine();
        s.AppendLine(Invariant($"Scan · started {started:HH:mm:ss}, ended {started + report.Elapsed:HH:mm:ss}"));
        s.AppendLine(Invariant($"  Length:      {Minutes(report.Elapsed)} of {Minutes(requested)}{(stoppedEarly ? ", stopped early" : "")}"));
        s.AppendLine(Invariant($"  Stream:      {(report.StreamDetected ? $"{Minutes(report.Summary.Duration)} streaming" : "none detected")}"));
        s.AppendLine(Invariant($"  Source:      {(report.UsedSteamLog ? "Steam VR Link log" : "pings only")}"));
        s.AppendLine(Invariant($"  Hiccups:     {report.Summary.Hiccups}"));
        foreach (var h in report.Hiccups)
        {
            s.AppendLine(Invariant($"    {started + h.Start:HH:mm:ss.fff}  peak {h.PeakMs:0} ms, total {h.TotalMs:0} ms"));
        }

        s.AppendLine(Invariant($"  Ping spikes: {report.PingOnlySpikes.Count} not counted"));
        foreach (var h in report.PingOnlySpikes)
        {
            s.AppendLine(Invariant($"    {started + h.Start:HH:mm:ss.fff}  peak {h.PeakMs:0} ms"));
        }

        foreach (var e in report.ProbeErrors)
        {
            s.AppendLine(Invariant($"  Probe error: {e}"));
        }

        s.AppendLine(Invariant($"  Chime:       {chimePlayed switch { true => "played", false => "failed", null => "not due" }}"));
        return s.ToString();
    }

    private void Write(string text, bool prune)
    {
        if (_path is null)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(directory);
            File.AppendAllText(_path, text);
            if (prune)
            {
                foreach (var old in new DirectoryInfo(directory).GetFiles("run-*.txt").OrderByDescending(f => f.Name).Skip(keep))
                {
                    old.Delete();
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // A log is a nice-to-have; a locked or read-only folder must not break a scan.
        }
    }

    private static string Minutes(TimeSpan t) => Invariant($"{(int)t.TotalMinutes}:{t.Seconds:00}");

    private static string Invariant(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);
}
