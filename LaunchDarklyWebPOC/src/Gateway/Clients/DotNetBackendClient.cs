using System.Net.Http.Json;
using Gateway.Models;

namespace Gateway.Clients;

/// <summary>
/// Typed HTTP client for the .NET backend service.
///
/// The HttpClient instance is injected by HttpClientFactory with the named
/// configuration "DotNetBackendClient" (base address, timeouts, headers).
/// We never call `new HttpClient()` here.
/// </summary>
public sealed class DotNetBackendClient : IDotNetBackendClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DotNetBackendClient> _logger;

    // Named client — registered in Program.cs as "DotNetBackendClient"
    public DotNetBackendClient(HttpClient httpClient, ILogger<DotNetBackendClient> logger)
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
            ".NET backend client calling: {BaseAddress}{Url}",
            _httpClient.BaseAddress, url);

        var response = await _httpClient.GetAsync(url, cancellationToken);

        // Surface HTTP errors as exceptions — the controller's error handling
        // will catch these and return ProblemDetails to the caller.
        response.EnsureSuccessStatusCode();

        var order = await response.Content.ReadFromJsonAsync<OrderResponse>(
            cancellationToken: cancellationToken);

        return order
            ?? throw new InvalidOperationException(
                "DotNetBackend returned null or unparseable response.");
    }
}
