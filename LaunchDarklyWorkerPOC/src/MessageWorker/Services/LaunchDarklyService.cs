using LaunchDarkly.Sdk;
using LaunchDarkly.Sdk.Server;
using LaunchDarklyPOC.MessageWorker.Interfaces;
using LaunchDarklyPOC.Shared.Constants;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

// Alias our worker options to avoid collision with LaunchDarkly.Sdk.Server.Configuration
using LaunchDarklyWorkerOptions = LaunchDarklyPOC.MessageWorker.Configuration.LaunchDarklyOptions;

namespace LaunchDarklyPOC.MessageWorker.Services;

public sealed class LaunchDarklyService : ILaunchDarklyService, IDisposable
{
    private readonly LdClient _client;
    private readonly ILogger<LaunchDarklyService> _logger;

    public LaunchDarklyService(
        IOptions<LaunchDarklyWorkerOptions> options,
        ILogger<LaunchDarklyService> logger)
    {
        _logger = logger;
        var opts = options.Value;

        // Use the fully-qualified LaunchDarkly Configuration type to avoid
        // ambiguity with our LaunchDarklyPOC.MessageWorker.Configuration namespace.
        var ldConfig = LaunchDarkly.Sdk.Server.Configuration
            .Builder(opts.SdkKey)
            .StartWaitTime(TimeSpan.FromSeconds(opts.StartWaitTimeSeconds))
            .Build();

        _client = new LdClient(ldConfig);

        if (_client.Initialized)
            _logger.LogInformation("LaunchDarkly SDK initialized successfully.");
        else
            _logger.LogWarning(
                "LaunchDarkly SDK did not initialize within {Timeout}s. Evaluations will use default values.",
                opts.StartWaitTimeSeconds);
    }

    public bool IsInitialized => _client.Initialized;

    public string EvaluateRoutingFlag(string accountId)
    {
        // accountId is the context key. LaunchDarkly applies consistent hashing on this key
        // for percentage rollouts, guaranteeing the SAME accountId → SAME variation every time.
        var context = Context.New(accountId);

        var variation = _client.StringVariation(
            AppConstants.FeatureFlagKey,
            context,
            AppConstants.DefaultVariation);

        _logger.LogDebug(
            "LD eval | FlagKey={FlagKey} AccountId={AccountId} → {Variation}",
            AppConstants.FeatureFlagKey, accountId, variation);

        return variation;
    }

    public void Dispose() => _client.Dispose();
}
