namespace Gateway.Options;

/// <summary>
/// Strongly-typed configuration for LaunchDarkly SDK.
/// Bound from the "LaunchDarkly" section in appsettings.json.
///
/// Using the Options Pattern (IOptions&lt;T&gt;) ensures:
///  - No magic strings scattered across the codebase.
///  - Validation can be added via DataAnnotations.
///  - Easy to mock/override in tests.
/// </summary>
public sealed class LaunchDarklyOptions
{
    /// <summary>
    /// Configuration section key used when calling services.Configure&lt;T&gt;.
    /// </summary>
    public const string SectionName = "LaunchDarkly";

    /// <summary>
    /// The LaunchDarkly Server-Side SDK Key.
    /// Obtain this from your LaunchDarkly project → Environments → SDK Key.
    ///
    /// IMPORTANT: Never hardcode this value.
    /// Set it in appsettings.json (dev), environment variables (staging/prod),
    /// or a secrets manager (Azure Key Vault, AWS Secrets Manager).
    ///
    /// For local development: use dotnet user-secrets or appsettings.Development.json.
    /// </summary>
    public string SdkKey { get; set; } = string.Empty;

    /// <summary>
    /// The LaunchDarkly feature flag key for backend routing.
    /// Default: "backend-routing"
    /// This must match the flag key you created in the LaunchDarkly dashboard.
    /// </summary>
    public string FeatureFlagKey { get; set; } = "backend-routing";

    /// <summary>
    /// The default variation to use when LaunchDarkly is unavailable.
    /// Defaulting to "python" means we fall back to the known-stable legacy backend.
    /// </summary>
    public string DefaultVariation { get; set; } = "python";
}
