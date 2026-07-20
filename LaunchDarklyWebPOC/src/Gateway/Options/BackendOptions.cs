namespace Gateway.Options;

/// <summary>
/// Strongly-typed configuration for downstream backend URLs.
/// Bound from the "Backends" section in appsettings.json.
///
/// Centralising URLs here means:
///  - A single place to change when backends move.
///  - Easy environment-specific overrides (Docker, Kubernetes, etc.).
///  - No magic strings in HttpClient registrations.
/// </summary>
public sealed class BackendOptions
{
    /// <summary>Configuration section key.</summary>
    public const string SectionName = "Backends";

    /// <summary>
    /// Base URL of the new .NET backend service.
    /// Example: http://localhost:5001
    /// </summary>
    public string DotNetBackendUrl { get; set; } = "http://localhost:5001";

    /// <summary>
    /// Base URL of the Python FastAPI backend (mock TIBCO).
    /// Example: http://localhost:8000
    /// </summary>
    public string PythonBackendUrl { get; set; } = "http://localhost:8000";
}
