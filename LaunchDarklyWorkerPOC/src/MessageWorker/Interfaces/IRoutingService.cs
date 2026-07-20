using LaunchDarklyPOC.MessageWorker.Models;
using LaunchDarklyPOC.Shared.Models;

namespace LaunchDarklyPOC.MessageWorker.Interfaces;

public interface IRoutingService
{
    Task<RoutingResult> RouteAsync(TransactionMessage message, CancellationToken cancellationToken = default);
}
