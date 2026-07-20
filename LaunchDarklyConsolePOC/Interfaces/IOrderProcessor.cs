using LaunchDarklyConsolePOC.Models;

namespace LaunchDarklyConsolePOC.Interfaces;

/// <summary>
/// Orchestrates the processing pipeline: evaluates the LaunchDarkly flag for each
/// order and dispatches to the matching <see cref="IDestinationService"/>.
/// </summary>
public interface IOrderProcessor
{
    /// <summary>
    /// Processes every order in the supplied list.
    /// Returns a result record for each order so that post-run validation
    /// and statistics can be computed from the complete dataset.
    /// </summary>
    Task<IReadOnlyList<ProcessingResult>> ProcessAsync(
        IReadOnlyList<Order> orders,
        CancellationToken cancellationToken = default);
}
