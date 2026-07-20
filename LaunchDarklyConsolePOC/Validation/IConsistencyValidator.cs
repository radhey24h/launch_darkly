using LaunchDarklyConsolePOC.Models;

namespace LaunchDarklyConsolePOC.Validation;

/// <summary>
/// Validates that the LaunchDarkly deterministic routing guarantee held for every
/// AccountId that appeared multiple times in the processed order set.
/// </summary>
public interface IConsistencyValidator
{
    /// <summary>
    /// Inspects all <paramref name="results"/> and checks that every AccountId
    /// received exactly one distinct variation across all its orders.
    /// Returns a <see cref="ValidationReport"/> with per-account breakdowns and
    /// an overall PASS / FAIL verdict.
    /// </summary>
    ValidationReport Validate(IReadOnlyList<ProcessingResult> results);
}
