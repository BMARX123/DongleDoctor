namespace DongleDoctor.Core.Checks;

/// <summary>
/// A check that evaluates one value read from the OS. Checks created together share a <see cref="Lazy{T}"/>,
/// so a group of checks reads the OS only once per run.
/// </summary>
internal sealed class SharedReadCheck<T>(string id, string name, Lazy<T> read, Func<T, Finding> evaluate) : ICheck
{
    public string Id => id;

    public string Name => name;

    public Task<Finding> RunAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => evaluate(read.Value), cancellationToken);
}
