using System.Net.Http.Json;
using Gateway.Models;

namespace Gateway.Clients;

/// <summary>
/// Typed HTTP client for the Python FastAPI backend (mock TIBCO).
///
/// The HttpClient instance is injected by HttpClientFactory with the named
/// configuration "PythonBackendClient" (base address, timeouts, headers).
/// </summary>
public sealed class PythonBackendClient : IPythonBackendClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PythonBackendClient> _logger;

    public PythonBackendClient(HttpClient httpClient, ILogger<PythonBackendClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<OrderResponse> GetOrderAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var url = $"/api/orders/{Uri.EscapeDataString(userId)}";

        _logger.LogDebug(
            "Python backend client calling: {BaseAddress}{Url}",
            _httpClient.BaseAddress, url);

        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var order = await response.Content.ReadFromJsonAsync<OrderResponse>(
            cancellationToken: cancellationToken);

        return order
            ?? throw new InvalidOperationException(
                "PythonBackend returned null or unparseable response.");
    }
}
