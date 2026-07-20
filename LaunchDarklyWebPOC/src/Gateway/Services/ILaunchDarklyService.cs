namespace Gateway.Services;

/// <summary>
/// Abstraction over the LaunchDarkly SDK for evaluating feature flags.
///
/// Why an interface?
///  - SOLID / Dependency Inversion: consumers depend on the abstraction, not LaunchDarkly.
///  - Testability: unit tests inject a mock without needing a real SDK connection.
///  - Future flexibility: swap providers (Flagsmith, Unleash, etc.) without changing callers.
/// </summary>
public interface ILaunchDarklyService
{
    /// <summary>
    /// Evaluates the backend-routing feature flag for the given user.
    ///
    /// LaunchDarkly computes a deterministic hash from (flagKey + userId) and maps
    /// it to a bucket 0-99. If that bucket falls within the rollout percentage
    /// configured for a variation, that variation is returned.
    ///
    /// Because the hash is deterministic:
    ///   - The same userId ALWAYS returns the same variation (sticky routing).
    ///   - Traffic distribution converges to configured percentages at scale.
    ///   - Changing the percentage shifts the cutoff, potentially moving some users.
    ///
    /// IMPORTANT: Pass a stable, meaningful key (userId, customerId, tenantId).
    /// NEVER pass Guid.NewGuid() — that would re-bucket the user on every request,
    /// defeating sticky routing entirely.
    /// </summary>
    /// <param name="userId">
    /// Stable identifier for the user/customer. Used as the LaunchDarkly context key.
    /// </param>
    /// <returns>
    /// The variation string: "dotnet" or "python".
    /// Returns the configured default variation if LaunchDarkly is unavailable.
    /// </returns>
    string GetBackendVariation(string userId);

    /// <summary>
    /// Returns true if the LaunchDarkly client is fully initialised and connected.
    /// Use this in health checks to surface SDK connectivity status.
    /// </summary>
    bool IsInitialized { get; }
}
