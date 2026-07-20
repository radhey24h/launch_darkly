namespace Gateway.Models;

/// <summary>
/// Unified order response returned by both backends.
/// Both the .NET backend and the Python FastAPI backend return this shape,
/// allowing the Gateway to forward the response without transformation.
/// </summary>
public sealed class OrderResponse
{
    /// <summary>
    /// Identifies which backend served the request.
    /// Value is either "dotnet" or "python".
    /// Useful for confirming canary routing during testing.
    /// </summary>
    public string Backend { get; set; } = string.Empty;

    /// <summary>A sample order identifier.</summary>
    public int OrderId { get; set; }

    /// <summary>Customer name associated with the order.</summary>
    public string Customer { get; set; } = string.Empty;

    /// <summary>Human-readable description of the response source.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>ISO-8601 UTC timestamp of when the response was generated.</summary>
    public string Timestamp { get; set; } = string.Empty;

    /// <summary>
    /// The userId that was used to evaluate the LaunchDarkly feature flag.
    /// Echo-ing this back helps verify sticky routing in tests.
    /// </summary>
    public string UserId { get; set; } = string.Empty;
}

/// <summary>
/// Envelope that the Gateway adds around the backend response,
/// enriched with routing metadata for observability.
/// </summary>
public sealed class GatewayOrderResponse
{
    /// <summary>The response forwarded from the selected backend.</summary>
    public OrderResponse Data { get; set; } = new();

    /// <summary>
    /// The LaunchDarkly variation that was evaluated for this request.
    /// "dotnet" or "python".
    /// </summary>
    public string SelectedVariation { get; set; } = string.Empty;

    /// <summary>Total time (ms) taken to evaluate the flag and call the backend.</summary>
    public long ElapsedMs { get; set; }

    /// <summary>
    /// Correlation ID propagated from the incoming request.
    /// Use this to trace a request across Gateway → Backend → Logs.
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;
}
