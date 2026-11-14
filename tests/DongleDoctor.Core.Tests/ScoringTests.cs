using DongleDoctor.Core.Checks;

namespace DongleDoctor.Core.Tests;

public class ScoringTests
{
    private static readonly Finding Pass = new("a", Severity.Pass, "ok", "");
    private static readonly Finding Info = new("b", Severity.Info, "skipped", "");
    private static readonly Finding Warning = new("c", Severity.Warning, "hub", "", FindingAction.Guide);
    private static readonly Finding Blocker = new("d", Severity.Blocker, "no dongle", "", FindingAction.Guide);

    private static ScanSummary Scan(int hiccups, bool pcCause = false) => new(hiccups, TimeSpan.FromMinutes(5), pcCause);

    [Fact]
    public void AllPass_IsReady()
    {
        var a = Scoring.Assess([Pass, Pass]);
        Assert.Equal(Verdict.Ready, a.Verdict);
        Assert.Equal(2, a.ChecksPassed);
        Assert.Equal(2, a.ChecksRun);
    }

    [Fact]
    public void Warning_StaysReady_AsImprovement()
    {
        var a = Scoring.Assess([Pass, Warning]);
        Assert.Equal(Verdict.Ready, a.Verdict);
        Assert.Equal([Warning], a.Improvements);
        Assert.Empty(a.ToFix);
    }

    [Fact]
    public void Blocker_IsNotReady_BlockersFirst()
    {
        var a = Scoring.Assess([Warning, Info, Blocker]);
        Assert.Equal(Verdict.NotReady, a.Verdict);
        Assert.Equal([Blocker, Warning], a.ToFix);
    }

    [Fact]
    public void Blocker_WinsOverScan() =>
        Assert.Equal(Verdict.NotReady, Scoring.Assess([Blocker], Scan(10)).Verdict);

    [Theory]
    [InlineData(0, Verdict.Ready)]
    [InlineData(1, Verdict.Ready)]
    [InlineData(2, Verdict.HiccupsNotFromPc)]
    public void Hiccups_WithoutPcCause(int hiccups, Verdict expected) =>
        Assert.Equal(expected, Scoring.Assess([Pass], Scan(hiccups)).Verdict);

    [Fact]
    public void Hiccups_WithPcCause_IsNotReady()
    {
        var a = Scoring.Assess([Pass, Warning], Scan(6, pcCause: true));
        Assert.Equal(Verdict.NotReady, a.Verdict);
        Assert.Equal([Warning], a.ToFix);
    }

    [Fact]
    public void HiccupsNotFromPc_KeepsWarningsAsImprovements() =>
        Assert.Equal([Warning], Scoring.Assess([Warning], Scan(6)).Improvements);

    [Fact]
    public void RateScalesWithDuration()
    {
        // 2 hiccups in 15 minutes is 0.67 per 5 minutes: Ready.
        Assert.Equal(Verdict.Ready, Scoring.Assess([Pass], new ScanSummary(2, TimeSpan.FromMinutes(15), false)).Verdict);
    }

    [Fact]
    public void ZeroLengthScan_DoesNotCountAsHiccups() =>
        Assert.Equal(Verdict.Ready, Scoring.Assess([Pass], new ScanSummary(3, TimeSpan.Zero, false)).Verdict);
}
