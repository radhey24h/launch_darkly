using LaunchDarkly.Logging;   // brings ILogAdapterExtensions.Level() into scope
using LaunchDarkly.Sdk;
using LaunchDarkly.Sdk.Server;
using LaunchDarklyConsolePOC.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

// Aliases resolve two naming conflicts in this file:
//   1. Project's own 'Configuration' folder vs LaunchDarkly.Sdk.Server.Configuration class.
//   2. Microsoft.Extensions.Logging.LogLevel vs LaunchDarkly.Logging.LogLevel.
using LdConfig   = LaunchDarkly.Sdk.Server.Configuration;
using LdLogLevel = LaunchDarkly.Logging.LogLevel;

namespace LaunchDarklyConsolePOC.Routing;

/// <summary>
/// Wraps the LaunchDarkly Server SDK as a singleton routing service.
///
/// Key design decisions:
///   - Singleton lifetime: one LdClient per application; the SDK maintains its own
///     connection pool and streaming connection to LaunchDarkly.
///   - Context kind "account": flags are evaluated against the AccountId context,
///     matching how the Gateway and MessageWorker POCs are configured.
///   - Disposable: LdClient holds a streaming connection; the DI container calls
///     Dispose() when the host shuts down, flushing any pending analytics events.
/// </summary>
public sealed class LaunchDarklyRoutingService : IRoutingService
{
    private readonly LdClient _client;
    private readonly string _featureFlagKey;
    private readonly string _defaultVariation;
    private readonly ILogger<LaunchDarklyRoutingService> _logger;
    private bool _disposed;

    public LaunchDarklyRoutingService(
        IOptions<LaunchDarklyOptions> options,
        ILogger<LaunchDarklyRoutingService> logger)
    {
        _logger = logger;
        var opts = options.Value;

        _featureFlagKey   = opts.FeatureFlagKey;
        _defaultVariation = opts.DefaultVariation;

        // Build SDK configuration.  StartWaitTime controls how long the constructor
        // blocks while the SDK fetches the initial flag state from LaunchDarkly.
        // After that timeout the SDK is still usable; it continues streaming updates.
        //
        // Suppress the SDK's own console logger entirely.
        // Every SDK state transition that matters to the demo is already
        // surfaced via _client.Initialized (checked below) and through
        // the ILogger.LogWarning / LogInformation calls.
        // Keeping the SDK silent prevents interleaved SDK WARN/ERROR lines
        // from breaking the per-order output during presentations.
        var config = LdConfig.Builder(opts.SdkKey)
            .StartWaitTime(TimeSpan.FromSeconds(opts.StartWaitTimeSeconds))
            .Logging(Logs.None)
            .Build();

        _client = new LdClient(config);

        if (_client.Initialized)
            _logger.LogInformation(
                "LaunchDarkly SDK initialized successfully. Flag='{Flag}'",
                _featureFlagKey);
        else
            _logger.LogWarning(
                "LaunchDarkly SDK did not fully initialize within the wait period. " +
                "The SDK may be offline or the SDK key may be invalid. " +
                "All flag evaluations will return the default variation '{Default}'.",
                _defaultVariation);
    }

    /// <inheritdoc />
    public string EvaluateVariation(string accountId)
    {
        // Context.New(key) creates a single-kind context with the default kind "user".
        // This matches exactly how LaunchDarklyWorkerPOC evaluates the same flag:
        //   WorkerPOC → Context.New(accountId)
        // The middleware-routing flag in LaunchDarkly is configured to roll out
        // "By user key", so the context kind must be "user" for the percentage split
        // (80% Python / 20% DotNet) to apply correctly.
        var context = Context.New(accountId);

        return _client.StringVariation(_featureFlagKey, context, _defaultVariation);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        // Flush queued analytics events before closing the streaming connection.
        _client.Flush();
        _client.Dispose();
        _disposed = true;
    }
}
