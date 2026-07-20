using System.Text;
using LaunchDarklyPOC.XmlProducer.Configuration;
using LaunchDarklyPOC.XmlProducer.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace LaunchDarklyPOC.XmlProducer.Services;

public sealed class RabbitMqPublisher : IMessagePublisher, IDisposable
{
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqPublisher> _logger;
    private IConnection? _connection;
    private IModel? _channel;

    public RabbitMqPublisher(IOptions<RabbitMqOptions> options, ILogger<RabbitMqPublisher> logger)
    {
        _options = options.Value;
        _logger = logger;
        ConnectWithRetry();
    }

    private void ConnectWithRetry()
    {
        var factory = new ConnectionFactory
        {
            HostName = _options.Host,
            Port = _options.Port,
            UserName = _options.Username,
            Password = _options.Password
        };

        for (int attempt = 1; attempt <= _options.MaxConnectionRetries; attempt++)
        {
            try
            {
                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();

                _channel.QueueDeclare(
                    queue: _options.QueueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);

                _logger.LogInformation(
                    "RabbitMQ publisher connected to {Host}:{Port}. Queue={QueueName}",
                    _options.Host, _options.Port, _options.QueueName);
                return;
            }
            catch (Exception ex) when (attempt < _options.MaxConnectionRetries)
            {
                _logger.LogWarning(ex,
                    "RabbitMQ connection attempt {Attempt}/{Max} failed. Retrying in 5s...",
                    attempt, _options.MaxConnectionRetries);
                Thread.Sleep(5000);
            }
        }

        throw new InvalidOperationException(
            $"Failed to connect to RabbitMQ at {_options.Host}:{_options.Port} after {_options.MaxConnectionRetries} attempts.");
    }

    public Task PublishAsync(string xmlMessage, string messageId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var body = Encoding.UTF8.GetBytes(xmlMessage);

        var properties = _channel!.CreateBasicProperties();
        properties.Persistent = true;
        properties.MessageId = messageId;
        properties.ContentType = "application/xml";
        properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        _channel.BasicPublish(
            exchange: string.Empty,
            routingKey: _options.QueueName,
            basicProperties: properties,
            body: body);

        _logger.LogDebug("Published message {MessageId} ({Bytes} bytes) to {QueueName}",
            messageId, body.Length, _options.QueueName);

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _channel?.Close();
        _channel?.Dispose();
        _connection?.Close();
        _connection?.Dispose();
    }
}
