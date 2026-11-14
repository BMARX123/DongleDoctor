namespace DongleDoctor.Core.Checks;

/// <summary>How much a finding matters. Blockers and warnings are what the UI shows as red and amber.</summary>
public enum Severity
{
    Pass,
    Info,
    Warning,
    Blocker,
}

/// <summary>What the user can do about a finding: let the app fix it, or follow a guide.</summary>
public enum FindingAction
{
    None,
    Fix,
    Guide,
}

/// <summary>The result of one check, in plain language. Technical detail goes in <see cref="Details"/>.</summary>
public sealed record Finding(
    string CheckId,
    Severity Severity,
    string Title,
    string Description,
    FindingAction Action = FindingAction.None,
    string? Details = null);
