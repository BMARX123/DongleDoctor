namespace DongleDoctor.Core.Checks;

public static class CheckRunner
{
    /// <summary>Runs a check, turning a failure into an Info finding so one broken check never stops the others.</summary>
    public static async Task<Finding> RunSafeAsync(ICheck check, CancellationToken cancellationToken = default)
    {
        try
        {
            return await check.RunAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return new Finding(check.Id, Severity.Info, "Couldn't run this check",
                "This doesn't mean anything is wrong.", Details: e.ToString());
        }
    }
}
