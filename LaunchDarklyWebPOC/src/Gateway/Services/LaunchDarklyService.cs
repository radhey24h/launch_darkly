using Gateway.Options;
using LaunchDarkly.Sdk;
using LaunchDarkly.Sdk.Server;
using LaunchDarkly.Sdk.Server.Interfaces;
using Microsoft.Extensions.Options;

namespace Gateway.Services;

/// <summary>
/// Singleton service wrapping the LaunchDarkly Server-Side SDK.
///
/// Singleton lifetime is REQUIRED by the LaunchDarkly SDK:
///  - The SDK maintains an in-memory flag store (streamed from LD servers).
///  - It manages persistent HTTP/streaming connections to LaunchDarkly.
///  - Creating multiple instances wastes connections and flag-store memory.
///  - The SDK is thread-safe and designed to be shared across all requests.
///
/// Lifecycle:
///  1. Constructed once at application start.
///  2. Initialises the LdClient which streams flag data from LaunchDarkly.
///  3. Evaluates flags on every request from the in-memory store (microseconds).
///  4. Disposed gracefully when the application shuts down (flushes event queue).
/// </summary>
public sealed class LaunchDarklyService : ILaunchDarklyService, IDisposable
{
    private readonly LdClient _client;
    private readonly LaunchDarklyOptions _options;
    private readonly ILogger<LaunchDarklyService> _logger;

    /// <summary>
    /// Initialises the LaunchDarkly client.
    /// The SDK key is injected via IOptions (never hardcoded).
    /// </summary>
    public LaunchDarklyService(
        IOptions<LaunchDarklyOptions> options,
        ILogger<LaunchDarklyService> logger)
    {
        _options = options.Value;
        _logger = logger;

        // Validate SDK key early — fail fast at startup rather than silently using fallback.
        if (string.IsNullOrWhiteSpace(_options.SdkKey))
        {
            _logger.LogCritical(
                "LaunchDarkly SDK key is not configured. " +
                "Set LaunchDarkly:SdkKey in appsettings.json or as an environment variable.");
        }

        // Configuration builder: tune streaming, timeouts, logging, etc.
        var config = Configuration.Builder(_options.SdkKey)
            // StartWaitTime: how long the constructor blocks waiting for initial flag sync.
            // 5 seconds is sufficient for most environments; increase if network is slow.
            .StartWaitTime(TimeSpan.FromSeconds(5))
            .Build();

        _client = new LdClient(config);

        if (_client.Initialized)
        {
            _logger.LogInformation(
                "LaunchDarkly SDK initialized successfully. " +
                "Flag: {FlagKey}, Default: {Default}",
                _options.FeatureFlagKey,
                _options.DefaultVariation);
        }
        else
        {
            _logger.LogWarning(
                "LaunchDarkly SDK did not initialize within the timeout. " +
                "Feature flag evaluations will use the default variation: {Default}. " +
                "The SDK will continue to retry in the background.",
                _options.DefaultVariation);
        }
    }

    /// <inheritdoc />
    public bool IsInitialized => _client.Initialized;

    /// <inheritdoc />
    public string GetBackendVariation(string userId)
    {
        // =============================================================
        // CONTEXT CREATION — This is the most important part.
        // =============================================================
        //
        // LaunchDarkly uses a "Context" to identify who is making the request.
        // The context key drives deterministic hashing for percentage rollouts.
        //
        // HOW DETERMINISTIC HASHING WORKS:
        //   hash = murmurhash3( flagKey + "." + contextKey )
        //   bucket = hash % 100          // 0-99
        //   if bucket < rolloutPercent   → variation A (e.g. "dotnet" at 10%)
        //   else                         → variation B (e.g. "python" at 90%)
        //
        // Because the same contextKey always produces the same hash:
        //   - "alice"   → bucket 6  → always "python"  (falls in 10–99)
        //   - "bob"     → bucket 12 → always "python"
        //   - "charlie" → bucket 4  → always "dotnet"  (falls in 0–9)
        //
        // NEVER use Guid.NewGuid() as the key:
        //   - Every request generates a new GUID → different bucket every time.
        //   - The same user would randomly hit either backend.
        //   - You lose sticky routing, making rollback impossible to reason about.
        //
        // GOOD keys: userId, customerId, tenantId, sessionId (if stable).
        // BAD keys: Guid.NewGuid(), DateTime.Now.Ticks, random numbers.
        //
        var context = Context.Builder(userId)
            .Build();

        // Evaluate the string variation.
        // Third argument = default value if SDK is offline/flag missing.
        var variation = _client.StringVariation(
            _options.FeatureFlagKey,
            context,
            _options.DefaultVariation);

        _logger.LogDebug(
            "LaunchDarkly evaluated flag '{Flag}' for user '{UserId}': variation = '{Variation}'",
            _options.FeatureFlagKey, userId, variation);

        return variation;
    }

    /// <summary>
    /// Gracefully dispose the LaunchDarkly client.
    /// This flushes any queued analytics events to LaunchDarkly before exit.
    /// </summary>
    public void Dispose()
    {
        _client.Dispose();
        _logger.LogInformation("LaunchDarkly SDK disposed gracefully.");
    }
}
