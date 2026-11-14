using DongleDoctor.Core.Checks;

namespace DongleDoctor.Core.Tests;

public class CheckRunnerTests
{
    [Fact]
    public async Task ThrowingCheck_BecomesInfoFinding()
    {
        var f = await CheckRunner.RunSafeAsync(new Throwing(new InvalidOperationException("boom")));
        Assert.Equal(Severity.Info, f.Severity);
        Assert.Equal("test.throws", f.CheckId);
        Assert.Contains("boom", f.Details);
    }

    [Fact]
    public async Task Cancellation_Propagates() =>
        await Assert.ThrowsAsync<OperationCanceledException>(() => CheckRunner.RunSafeAsync(new Throwing(new OperationCanceledException())));

    private sealed class Throwing(Exception e) : ICheck
    {
        public string Id => "test.throws";

        public string Name => "Throws";

        public Task<Finding> RunAsync(CancellationToken cancellationToken = default) => Task.FromException<Finding>(e);
    }
}
