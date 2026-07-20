using Gateway.Models;

namespace Gateway.Clients;

/// <summary>
/// Typed HTTP client interface for communicating with the Python FastAPI backend
/// (mock TIBCO legacy system).
///
/// See <see cref="IDotNetBackendClient"/> for the rationale behind using typed clients.
/// </summary>
public interface IPythonBackendClient
{
    /// <summary>
    /// Calls GET /api/orders/{userId} on the Python FastAPI backend.
    /// </summary>
    Task<OrderResponse> GetOrderAsync(string userId, CancellationToken cancellationToken = default);
}
