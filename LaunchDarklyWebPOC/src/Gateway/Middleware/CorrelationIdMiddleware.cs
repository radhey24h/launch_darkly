namespace Gateway.Middleware;

/// <summary>
/// Middleware that ensures every request has a Correlation ID.
///
/// A Correlation ID is a unique identifier that flows through all systems
/// involved in handling a single logical request. It enables:
///   - End-to-end request tracing across Gateway → Backend → Logs.
///   - Log correlation: grep by CorrelationId to see all log lines for one request.
///   - Distributed tracing: pass X-Correlation-Id header to downstream services.
///
/// Behaviour:
///   1. If the incoming request contains an "X-Correlation-Id" header, reuse it.
///      (Allows the client or an upstream proxy to set the ID.)
///   2. If not present, generate a new GUID and attach it.
///   3. Store the ID in HttpContext.Items for use by controllers and services.
///   4. Echo the ID back in the response header so callers can correlate.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    private const string CorrelationIdHeader = "X-Correlation-Id";

    // Key used to store the correlation ID in HttpContext.Items
    public const string CorrelationIdKey = "CorrelationId";

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Try to read an existing correlation ID from the request headers.
        // This supports scenarios where an API gateway or load balancer upstream
        // already attached a correlation ID (common in enterprise architectures).
        var correlationId = context.Request.Headers[CorrelationIdHeader].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(correlationId))
        {
            // No upstream ID — generate one. NewGuid() is appropriate here
            // because we WANT a unique ID per request (unlike feature flag context keys).
            correlationId = Guid.NewGuid().ToString("N");
            _logger.LogDebug("Generated new Correlation ID: {CorrelationId}", correlationId);
        }
        else
        {
            _logger.LogDebug("Reusing upstream Correlation ID: {CorrelationId}", correlationId);
        }

        // Store in HttpContext.Items so any downstream middleware/controller can access it.
        context.Items[CorrelationIdKey] = correlationId;

        // Echo back to the client in the response header.
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationIdHeader] = correlationId;
            return Task.CompletedTask;
        });

        await _next(context);
    }
}
