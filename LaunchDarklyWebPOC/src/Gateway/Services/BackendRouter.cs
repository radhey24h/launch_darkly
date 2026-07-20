using System.Diagnostics;
using Gateway.Clients;
using Gateway.Models;

namespace Gateway.Services;

/// <summary>
/// Core routing logic: evaluates the LaunchDarkly feature flag and dispatches
/// the request to either the .NET backend or the Python (mock TIBCO) backend.
///
/// This class is the heart of the canary deployment pattern:
///   - It does NOT know about HTTP endpoints, controllers, or middleware.
///   - It only cares about (1) flag evaluation, (2) client dispatch, (3) response enrichment.
///   - This makes it fully unit-testable without network calls.
/// </summary>
public sealed class BackendRouter : IBackendRouter
{
    private readonly ILaunchDarklyService _launchDarklyService;
    private readonly IDotNetBackendClient _dotNetClient;
    private readonly IPythonBackendClient _pythonClient;
    private readonly ILogger<BackendRouter> _logger;

    // Variation string constants — must match what's configured in LaunchDarkly dashboard.
    private const string DotNetVariation = "dotnet";
    private const string PythonVariation = "python";

    public BackendRouter(
        ILaunchDarklyService launchDarklyService,
        IDotNetBackendClient dotNetClient,
        IPythonBackendClient pythonClient,
        ILogger<BackendRouter> logger)
    {
        _launchDarklyService = launchDarklyService;
        _dotNetClient = dotNetClient;
        _pythonClient = pythonClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<GatewayOrderResponse> RouteOrderRequestAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        // Start a stopwatch so we can report end-to-end latency to the caller.
        var sw = Stopwatch.StartNew();

        // ---------------------------------------------------------------
        // Step 1: Evaluate feature flag
        // This is an in-memory operation (< 1 ms) once the SDK is initialised.
        // The SDK continuously streams flag changes from LaunchDarkly, so
        // percentage changes take effect within seconds without redeployment.
        // ---------------------------------------------------------------
        var variation = _launchDarklyService.GetBackendVariation(userId);

        _logger.LogInformation(
            "Routing decision: UserId={UserId} → Variation={Variation}",
            userId, variation);

        // ---------------------------------------------------------------
        // Step 2: Dispatch to the selected backend
        // ---------------------------------------------------------------
        OrderResponse backendResponse;

        if (string.Equals(variation, DotNetVariation, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation(
                "Forwarding to .NET backend for UserId={UserId}", userId);
            backendResponse = await _dotNetClient.GetOrderAsync(userId, cancellationToken);
        }
        else
        {
            // Default / Python branch — covers both "python" variation and any
            // unexpected variation value (defensive fallback).
            _logger.LogInformation(
                "Forwarding to Python backend (mock TIBCO) for UserId={UserId}", userId);
            backendResponse = await _pythonClient.GetOrderAsync(userId, cancellationToken);
        }

        sw.Stop();

        _logger.LogInformation(
            "Request completed: UserId={UserId}, Backend={Backend}, ElapsedMs={ElapsedMs}",
            userId, backendResponse.Backend, sw.ElapsedMilliseconds);

        // ---------------------------------------------------------------
        // Step 3: Wrap response with routing metadata
        // The envelope lets API consumers know which backend was used,
        // useful for canary validation dashboards and observability.
        // ---------------------------------------------------------------
        return new GatewayOrderResponse
        {
            Data = backendResponse,
            SelectedVariation = variation,
            ElapsedMs = sw.ElapsedMilliseconds,
            // Correlation ID is set by CorrelationIdMiddleware and available
            // in HttpContext.Items — surfaced here via the response model.
            CorrelationId = string.Empty // populated by controller from HttpContext
        };
    }
}
