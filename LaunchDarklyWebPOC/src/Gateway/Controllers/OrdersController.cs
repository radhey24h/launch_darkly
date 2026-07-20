using Gateway.Middleware;
using Gateway.Services;
using Microsoft.AspNetCore.Mvc;

namespace Gateway.Controllers;

/// <summary>
/// Gateway controller for order requests.
///
/// Responsibilities (thin controller pattern):
///   1. Accept the HTTP request and validate inputs.
///   2. Extract Correlation ID from middleware-populated HttpContext.Items.
///   3. Delegate routing to IBackendRouter.
///   4. Return the result or a structured error (ProblemDetails).
///
/// The controller does NOT contain any business logic or routing decisions.
/// All routing is encapsulated in BackendRouter, which is independently testable.
/// </summary>
[ApiController]
[Route("api")]
[Produces("application/json")]
public sealed class OrdersController : ControllerBase
{
    private readonly IBackendRouter _backendRouter;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(IBackendRouter backendRouter, ILogger<OrdersController> logger)
    {
        _backendRouter = backendRouter;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves an order for the specified user.
    ///
    /// The LaunchDarkly feature flag "backend-routing" is evaluated using the userId
    /// as the context key. Based on the configured percentage rollout:
    ///   - 10% of users are routed to the new .NET backend.
    ///   - 90% of users are routed to the Python backend (mock TIBCO).
    ///
    /// Routing is sticky: the same userId always gets the same backend
    /// due to LaunchDarkly's deterministic hashing algorithm.
    /// </summary>
    /// <param name="userId">
    /// The user identifier. This is used as the LaunchDarkly context key.
    /// Must be a stable, non-random value.
    ///
    /// Sample users and their expected routing (with 10% dotnet rollout):
    ///   - "alice"   → python  (bucket ~53, falls in 10-99 range)
    ///   - "bob"     → python  (bucket ~78, falls in 10-99 range)
    ///   - "charlie" → python  (bucket ~41, falls in 10-99 range)
    ///   - "david"   → python  (bucket ~65, falls in 10-99 range)
    ///   - "emma"    → python  (bucket ~29, falls in 10-99 range)
    ///
    /// NOTE: Actual bucket values depend on the LaunchDarkly SDK's hashing
    /// of (flagKey + userId). The above are illustrative.
    /// To verify which backend a user gets: call this endpoint and inspect
    /// the "selectedVariation" field in the response.
    /// </param>
    /// <param name="cancellationToken">Injected by ASP.NET Core framework.</param>
    /// <returns>Order data enriched with routing metadata.</returns>
    /// <response code="200">Order retrieved successfully.</response>
    /// <response code="400">Invalid userId.</response>
    /// <response code="502">Downstream backend returned an error.</response>
    /// <response code="503">Backend unavailable.</response>
    [HttpGet("orders/{userId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetOrder(
        [FromRoute] string userId,
        CancellationToken cancellationToken)
    {
        // Basic input validation
        if (string.IsNullOrWhiteSpace(userId))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid userId",
                Detail = "userId must be a non-empty string.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        // Extract Correlation ID set by CorrelationIdMiddleware.
        var correlationId = HttpContext.Items[CorrelationIdMiddleware.CorrelationIdKey] as string
                            ?? "unknown";

        _logger.LogInformation(
            "[{CorrelationId}] Processing order request for UserId={UserId}",
            correlationId, userId);

        try
        {
            var result = await _backendRouter.RouteOrderRequestAsync(userId, cancellationToken);

            // Inject the correlation ID into the response envelope.
            result.CorrelationId = correlationId;

            _logger.LogInformation(
                "[{CorrelationId}] Order response: UserId={UserId}, Backend={Backend}, " +
                "Variation={Variation}, ElapsedMs={ElapsedMs}",
                correlationId,
                userId,
                result.Data.Backend,
                result.SelectedVariation,
                result.ElapsedMs);

            return Ok(result);
        }
        catch (HttpRequestException ex)
        {
            // The selected backend is unreachable or returned an error.
            _logger.LogError(ex,
                "[{CorrelationId}] Backend unreachable for UserId={UserId}: {Message}",
                correlationId, userId, ex.Message);

            return StatusCode(StatusCodes.Status502BadGateway, new ProblemDetails
            {
                Title = "Backend Unavailable",
                Detail = $"The downstream service returned an error: {ex.Message}",
                Status = StatusCodes.Status502BadGateway,
                Extensions = { ["correlationId"] = correlationId }
            });
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Client disconnected — not an error on our side.
            _logger.LogWarning(
                "[{CorrelationId}] Request cancelled by client for UserId={UserId}",
                correlationId, userId);

            return StatusCode(StatusCodes.Status499ClientClosedRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[{CorrelationId}] Unexpected error for UserId={UserId}",
                correlationId, userId);

            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Title = "Service Error",
                Detail = "An unexpected error occurred. Please try again.",
                Status = StatusCodes.Status503ServiceUnavailable,
                Extensions = { ["correlationId"] = correlationId }
            });
        }
    }
}
