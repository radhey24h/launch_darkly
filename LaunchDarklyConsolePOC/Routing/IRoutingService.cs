namespace LaunchDarklyConsolePOC.Routing;

/// <summary>
/// Abstraction over the LaunchDarkly SDK for feature-flag evaluation.
/// Decouples business logic from the SDK type system, enabling unit testing.
/// </summary>
public interface IRoutingService : IDisposable
{
    /// <summary>
    /// Evaluates the configured feature flag for the given account and returns
    /// the variation string ("dotnet" or "python").
    ///
    /// The LaunchDarkly SDK deterministically hashes the context key, so calling
    /// this method multiple times with the same <paramref name="accountId"/> always
    /// returns the same variation (barring flag rule changes).
    /// </summary>
    string EvaluateVariation(string accountId);
}
