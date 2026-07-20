using System.Text;
using LaunchDarklyPOC.MessageWorker.Configuration;
using LaunchDarklyPOC.MessageWorker.Interfaces;
using LaunchDarklyPOC.Shared.Constants;
using LaunchDarklyPOC.Shared.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Serilog.Context;

namespace LaunchDarklyPOC.MessageWorker.Services;

public sealed class RabbitMqConsumer : IMessageConsumer, IDisposable
{
    private readonly RabbitMqOptions _options;
    private readonly IRoutingService _routingService;
    private readonly IXmlParser _xmlParser;
    private readonly ILogger<RabbitMqConsumer> _logger;

    private IConnection? _connection;
    private IModel? _channel;

    public RabbitMqConsumer(
        IOptions<RabbitMqOptions> options,
        IRoutingService routingService,
        IXmlParser xmlParser,
        ILogger<RabbitMqConsumer> logger)
    {
        _options = options.Value;
        _routingService = routingService;
        _xmlParser = xmlParser;
        _logger = logger;
    }

    public async Task StartConsumingAsync(CancellationToken cancellationToken)
    {
        await ConnectWithRetryAsync(cancellationToken);

        _channel!.QueueDeclare(
            queue: _options.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        _channel.BasicQos(prefetchSize: 0, prefetchCount: _options.PrefetchCount, global: false);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += OnMessageReceivedAsync;

        _channel.BasicConsume(
            queue: _options.QueueName,
            autoAck: false,
            consumer: consumer);

        _logger.LogInformation(
            "Consumer started. Queue={QueueName} PrefetchCount={PrefetchCount}",
            _options.QueueName, _options.PrefetchCount);

        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Consumer shutting down gracefully.");
        }
    }

    private async Task OnMessageReceivedAsync(object sender, BasicDeliverEventArgs ea)
    {
        var messageId = ea.BasicProperties.MessageId ?? Guid.NewGuid().ToString("N");
        var sw = System.Diagnostics.Stopwatch.StartNew();

        using var _ = LogContext.PushProperty("RabbitMqMessageId", messageId);

        try
        {
            var xml = Encoding.UTF8.GetString(ea.Body.ToArray());
            var message = _xmlParser.Deserialize(xml);

            using var _a = LogContext.PushProperty("AccountId", message.AccountId);
            using var _b = LogContext.PushProperty("CorrelationId", message.CorrelationId);
            using var _c = LogContext.PushProperty("TransactionId", message.TransactionId);

            _logger.LogInformation(
                "Received | MessageId={MessageId} AccountId={AccountId} TransactionId={TransactionId} Operation={Operation}",
                messageId, message.AccountId, message.TransactionId, message.Operation);

            var result = await _routingService.RouteAsync(message, CancellationToken.None);
            sw.Stop();

            _logger.LogInformation(
                "Processed | MessageId={MessageId} AccountId={AccountId} FeatureFlag={FlagKey} " +
                "Variation={Variation} Destination={Destination} HttpStatus={HttpStatus} " +
                "ProcessingTimeMs={ProcessingTimeMs} Success={Success}",
                messageId, message.AccountId, AppConstants.FeatureFlagKey,
                result.Variation, result.Destination, result.HttpStatusCode,
                sw.ElapsedMilliseconds, result.Success);

            _channel!.BasicAck(ea.DeliveryTag, multiple: false);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex,
                "Failed | MessageId={MessageId} ProcessingTimeMs={ProcessingTimeMs}",
                messageId, sw.ElapsedMilliseconds);

            _channel!.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
        }
    }

    private async Task ConnectWithRetryAsync(CancellationToken cancellationToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _options.Host,
            Port = _options.Port,
            UserName = _options.Username,
            Password = _options.Password,
            DispatchConsumersAsync = true
        };

        for (int attempt = 1; attempt <= _options.MaxConnectionRetries; attempt++)
        {
            try
            {
                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();

                _logger.LogInformation(
                    "Connected to RabbitMQ at {Host}:{Port}",
                    _options.Host, _options.Port);
                return;
            }
            catch (Exception ex) when (attempt < _options.MaxConnectionRetries && !cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex,
                    "RabbitMQ connection attempt {Attempt}/{Max} failed. Retrying in 5s...",
                    attempt, _options.MaxConnectionRetries);
                await Task.Delay(5000, cancellationToken);
            }
        }

        throw new InvalidOperationException(
            $"Could not connect to RabbitMQ at {_options.Host}:{_options.Port} after {_options.MaxConnectionRetries} attempts.");
    }

    public void Dispose()
    {
        _channel?.Close();
        _channel?.Dispose();
        _connection?.Close();
        _connection?.Dispose();
    }
}
