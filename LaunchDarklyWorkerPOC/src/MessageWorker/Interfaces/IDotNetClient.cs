using LaunchDarklyPOC.Shared.Models;

namespace LaunchDarklyPOC.MessageWorker.Interfaces;

public interface IDotNetClient
{
    Task<(int StatusCode, bool IsSuccess)> ProcessAsync(ProcessRequest request, CancellationToken cancellationToken = default);
}
