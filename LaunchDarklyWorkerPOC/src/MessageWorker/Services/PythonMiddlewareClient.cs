using System.Net.Http.Json;
using LaunchDarklyPOC.MessageWorker.Interfaces;
using LaunchDarklyPOC.Shared.Models;
using Microsoft.Extensions.Logging;

namespace LaunchDarklyPOC.MessageWorker.Services;

public sealed class PythonMiddlewareClient : IPythonClient
{
    private const string ClientName = "PythonMiddleware";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PythonMiddlewareClient> _logger;

    public PythonMiddlewareClient(IHttpClientFactory httpClientFactory, ILogger<PythonMiddlewareClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<(int StatusCode, bool IsSuccess)> ProcessAsync(
        ProcessRequest request, CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient(ClientName);

        using var response = await client.PostAsJsonAsync("/process", request, cancellationToken);

        _logger.LogInformation(
            "PythonMiddleware responded: StatusCode={StatusCode} TransactionId={TransactionId} CorrelationId={CorrelationId}",
            (int)response.StatusCode, request.TransactionId, request.CorrelationId);

        return ((int)response.StatusCode, response.IsSuccessStatusCode);
    }
}
