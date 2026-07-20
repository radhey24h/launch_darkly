namespace LaunchDarklyConsolePOC.Configuration;

/// <summary>
/// Strongly-typed representation of the LaunchDarkly section in appsettings.json.
/// Bound via the Options Pattern — zero hardcoded values in business logic.
/// </summary>
public sealed class LaunchDarklyOptions
{
    /// <summary>The configuration section key used in appsettings.json.</summary>
    public const string SectionName = "LaunchDarkly";

    /// <summary>LaunchDarkly Server-Side SDK key (starts with "sdk-").</summary>
    public string SdkKey { get; init; } = "sdk-YOUR-SDK-KEY-HERE";

    /// <summary>
    /// The feature flag key to evaluate.
    /// Must match an existing flag in your LaunchDarkly project.
    /// Default: "backend-routing" (same key used by LaunchDarklyWebPOC and LaunchDarklyWorkerPOC).
    /// </summary>
    public string FeatureFlagKey { get; init; } = "backend-routing";

    /// <summary>
    /// Variation returned when the SDK is offline or the flag is not found.
    /// Must be either "python" or "dotnet" (case-insensitive).
    /// </summary>
    public string DefaultVariation { get; init; } = "python";

    /// <summary>
    /// Maximum number of seconds to block while the SDK connects to LaunchDarkly
    /// and receives the initial flag state.  After this timeout the SDK continues
    /// in streaming mode; the client is usable but may still use defaults briefly.
    /// </summary>
    public int StartWaitTimeSeconds { get; init; } = 10;
}
