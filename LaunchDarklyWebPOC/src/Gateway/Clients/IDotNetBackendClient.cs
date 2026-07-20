using Gateway.Models;

namespace Gateway.Clients;

/// <summary>
/// Typed HTTP client interface for communicating with the .NET backend service.
///
/// Using a typed client (via HttpClientFactory) instead of new HttpClient() provides:
///  - Connection pooling: HttpClientFactory manages the underlying SocketsHttpHandler
///    lifecycle, avoiding socket exhaustion from creating new clients per request.
///  - DNS refresh: Handlers are recycled on a configurable interval (default 2 min),
///    so DNS changes (e.g. during Kubernetes rolling deploys) are picked up.
///  - Centralised configuration: Timeouts, retry policies, base address are all
///    defined once in DI registration, not scattered across call sites.
///  - Testability: Tests inject a mock of this interface without network calls.
/// </summary>
public interface IDotNetBackendClient
{
    /// <summary>
    /// Calls GET /api/orders/{userId} on the .NET backend service.
    /// </summary>
    /// <param name="userId">The user ID to fetch the order for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The order response from the .NET backend.</returns>
    Task<OrderResponse> GetOrderAsync(string userId, CancellationToken cancellationToken = default);
}
