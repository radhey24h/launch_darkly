using Gateway.Models;

namespace Gateway.Services;

/// <summary>
/// Abstraction for routing an incoming request to the correct backend.
///
/// The router encapsulates the decision logic:
///   1. Evaluate LaunchDarkly flag → get variation.
///   2. Dispatch to the appropriate backend client.
///   3. Return the enriched <see cref="GatewayOrderResponse"/>.
///
/// Separating routing logic from the controller keeps the controller thin
/// and makes the routing rules independently testable.
/// </summary>
public interface IBackendRouter
{
    /// <summary>
    /// Routes the order request to either the .NET or Python backend
    /// based on the LaunchDarkly feature flag evaluation for the given user.
    /// </summary>
    /// <param name="userId">
    /// The stable user identifier used to evaluate the feature flag.
    /// This must be a consistent value across requests for the same user.
    /// </param>
    /// <param name="cancellationToken">
    /// Propagates notification that the operation should be cancelled
    /// (e.g., client disconnected).
    /// </param>
    /// <returns>
    /// A <see cref="GatewayOrderResponse"/> containing the backend data
    /// plus routing metadata (variation, elapsed time, correlation ID).
    /// </returns>
    Task<GatewayOrderResponse> RouteOrderRequestAsync(
        string userId,
        CancellationToken cancellationToken = default);
}
