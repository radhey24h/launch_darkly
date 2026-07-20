using LaunchDarklyPOC.MessageWorker.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LaunchDarklyPOC.MessageWorker;

public sealed class Worker : BackgroundService
{
    private readonly IMessageConsumer _messageConsumer;
    private readonly ILaunchDarklyService _ldService;
    private readonly ILogger<Worker> _logger;

    public Worker(
        IMessageConsumer messageConsumer,
        ILaunchDarklyService ldService,
        ILogger<Worker> logger)
    {
        _messageConsumer = messageConsumer;
        _ldService = ldService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "MessageWorker starting. LaunchDarkly SDK initialized={IsInitialized}",
            _ldService.IsInitialized);

        await _messageConsumer.StartConsumingAsync(stoppingToken);

        _logger.LogInformation("MessageWorker stopped.");
    }
}
