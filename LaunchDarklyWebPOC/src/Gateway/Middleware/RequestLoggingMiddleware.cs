using System.Diagnostics;

namespace Gateway.Middleware;

/// <summary>
/// Middleware that logs every incoming HTTP request and its response.
///
/// Structured logging fields per request:
///   - Method, Path, QueryString
///   - Correlation ID (set by CorrelationIdMiddleware)
///   - Response Status Code
///   - Elapsed time in milliseconds
///
/// Placing this middleware AFTER CorrelationIdMiddleware ensures the
/// Correlation ID is always available when we log.
///
/// NOTE: For production use, consider using Serilog's UseSerilogRequestLogging()
/// which is more configurable and avoids double-logging with ASP.NET's built-in
/// request logging. This middleware is included for POC clarity and debuggability.
/// </summary>
public sealed class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Items[CorrelationIdMiddleware.CorrelationIdKey] as string
                            ?? "unknown";

        var sw = Stopwatch.StartNew();

        _logger.LogInformation(
            "[{CorrelationId}] → {Method} {Path}{Query}",
            correlationId,
            context.Request.Method,
            context.Request.Path,
            context.Request.QueryString);

        try
        {
            await _next(context);
        }
        finally
        {
            sw.Stop();

            // Log at Warning level if the response indicates an error,
            // so error requests are easier to filter in log aggregators.
            var level = context.Response.StatusCode >= 400
                ? LogLevel.Warning
                : LogLevel.Information;

            _logger.Log(level,
                "[{CorrelationId}] ← {StatusCode} {Method} {Path} ({ElapsedMs}ms)",
                correlationId,
                context.Response.StatusCode,
                context.Request.Method,
                context.Request.Path,
                sw.ElapsedMilliseconds);
        }
    }
}
