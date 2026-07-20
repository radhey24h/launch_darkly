namespace LaunchDarklyConsolePOC.Models;

/// <summary>
/// Result of the deterministic-routing consistency check.
///
/// LaunchDarkly uses a deterministic hashing algorithm on the context key so that
/// the same context key always maps to the same variation (unless the flag rules change).
/// This report proves that guarantee held throughout the run.
/// </summary>
public sealed class ValidationReport
{
    /// <summary>True when no AccountId received more than one distinct variation.</summary>
    public bool IsConsistent { get; init; }

    /// <summary>
    /// All variations received, keyed by AccountId.
    /// For repeated accounts the list will contain multiple identical entries (proving consistency).
    /// </summary>
    public required IReadOnlyDictionary<string, IReadOnlyList<string>> AccountVariations { get; init; }

    /// <summary>
    /// AccountIds where at least two different variations were observed.
    /// An empty list means PASS; a non-empty list means FAIL.
    /// </summary>
    public required IReadOnlyList<string> FailedAccounts { get; init; }
}
