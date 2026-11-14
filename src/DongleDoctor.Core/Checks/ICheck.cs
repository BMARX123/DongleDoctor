namespace DongleDoctor.Core.Checks;

/// <summary>A point-in-time inspection that produces one <see cref="Finding"/>.</summary>
public interface ICheck
{
    string Id { get; }

    /// <summary>What the Quick checks screen shows while the check runs.</summary>
    string Name { get; }

    Task<Finding> RunAsync(CancellationToken cancellationToken = default);
}
