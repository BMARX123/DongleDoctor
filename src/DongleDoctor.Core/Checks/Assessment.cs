namespace DongleDoctor.Core.Checks;

/// <summary>The overall result shown on the results screen.</summary>
public enum Verdict
{
    Ready,
    NotReady,

    /// <summary>Hiccups above the Ready rate, with nothing on the PC to blame or fix.</summary>
    HiccupsNotFromPc,
}

/// <summary>What a streaming scan measured.</summary>
/// <param name="HasPcCause">At least one hiccup lined up with something on the PC (a driver delay, a Wi-Fi scan…).</param>
public sealed record ScanSummary(int Hiccups, TimeSpan Duration, bool HasPcCause);

/// <param name="ToFix">Blockers then warnings, shown as a checklist when the verdict is Not ready.</param>
/// <param name="Improvements">Warnings that don't stop a Ready verdict.</param>
public sealed record Assessment(
    Verdict Verdict,
    IReadOnlyList<Finding> ToFix,
    IReadOnlyList<Finding> Improvements,
    int ChecksPassed,
    int ChecksRun,
    ScanSummary? Scan);

public static class Scoring
{
    /// <summary>Ready allows at most this many hiccups per 5 minutes. A placeholder until calibrated on more rigs.</summary>
    public const double ReadyHiccupsPerFiveMinutes = 1;

    /// <summary>Combines the instant-check findings and, once a scan has run, its summary into a verdict.</summary>
    public static Assessment Assess(IReadOnlyList<Finding> findings, ScanSummary? scan = null)
    {
        var blockers = findings.Where(f => f.Severity == Severity.Blocker).ToList();
        var warnings = findings.Where(f => f.Severity == Severity.Warning).ToList();
        var passed = findings.Count(f => f.Severity == Severity.Pass);

        Assessment Result(Verdict verdict, IReadOnlyList<Finding> toFix, IReadOnlyList<Finding> improvements) =>
            new(verdict, toFix, improvements, passed, findings.Count, scan);

        if (blockers.Count > 0)
        {
            return Result(Verdict.NotReady, [.. blockers, .. warnings], []);
        }

        if (scan is not null && TooManyHiccups(scan))
        {
            // With a PC-side cause, the warnings are the checklist; without one, there's nothing on the PC to fix.
            return scan.HasPcCause
                ? Result(Verdict.NotReady, warnings, [])
                : Result(Verdict.HiccupsNotFromPc, [], warnings);
        }

        return Result(Verdict.Ready, [], warnings);
    }

    internal static bool TooManyHiccups(ScanSummary scan)
    {
        if (scan.Duration <= TimeSpan.Zero)
        {
            return false;
        }

        return scan.Hiccups / scan.Duration.TotalMinutes * 5 > ReadyHiccupsPerFiveMinutes;
    }
}
