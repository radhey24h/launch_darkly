using LaunchDarklyConsolePOC.Models;

namespace LaunchDarklyConsolePOC.Validation;

/// <summary>
/// Checks that LaunchDarkly's deterministic routing guarantee held throughout the run.
///
/// LaunchDarkly's SDK hashes the (context kind + context key) pair to a percentage bucket
/// and then evaluates flag targeting rules against that bucket.  Because the hash is
/// deterministic, the same context key will always land in the same bucket — meaning the
/// same AccountId will always receive the same variation, as long as flag rules have not
/// changed between evaluations.
///
/// This validator groups all ProcessingResults by AccountId and verifies that each group
/// contains exactly one distinct variation.
/// </summary>
public sealed class ConsistencyValidator : IConsistencyValidator
{
    /// <inheritdoc />
    public ValidationReport Validate(IReadOnlyList<ProcessingResult> results)
    {
        // Group every variation received, keyed by AccountId.
        var accountVariations = results
            .GroupBy(r => r.Order.AccountId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<string>)g.Select(r => r.Variation).ToList(),
                StringComparer.OrdinalIgnoreCase);

        // An AccountId FAILS when it received two or more distinct variations.
        var failedAccounts = accountVariations
            .Where(kvp => kvp.Value
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() > 1)
            .Select(kvp => kvp.Key)
            .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ValidationReport
        {
            IsConsistent      = failedAccounts.Count == 0,
            AccountVariations = accountVariations,
            FailedAccounts    = failedAccounts
        };
    }
}
