using LaunchDarklyPOC.Shared.Interfaces;
using LaunchDarklyPOC.Shared.Models;
using LaunchDarklyPOC.XmlProducer.Configuration;
using LaunchDarklyPOC.XmlProducer.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LaunchDarklyPOC.XmlProducer.Services;

public sealed class ProducerService : BackgroundService
{
    private readonly IMessagePublisher _publisher;
    private readonly IXmlParser _xmlParser;
    private readonly RabbitMqOptions _options;
    private readonly ILogger<ProducerService> _logger;

    public ProducerService(
        IMessagePublisher publisher,
        IXmlParser xmlParser,
        IOptions<RabbitMqOptions> options,
        ILogger<ProducerService> logger)
    {
        _publisher = publisher;
        _xmlParser = xmlParser;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("XmlProducer started. Sending demo message batch...");

        while (!stoppingToken.IsCancellationRequested)
        {
            var batch = BuildDemoBatch();

            foreach (var (message, messageId) in batch)
            {
                if (stoppingToken.IsCancellationRequested) break;

                try
                {
                    var xml = _xmlParser.Serialize(message);
                    await _publisher.PublishAsync(xml, messageId, stoppingToken);

                    _logger.LogInformation(
                        "Published | MessageId={MessageId} AccountId={AccountId} TransactionId={TransactionId} " +
                        "Operation={Operation} Amount={Amount} {Currency}",
                        messageId, message.AccountId, message.TransactionId,
                        message.Operation, message.Amount, message.Currency);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to publish message {MessageId} for AccountId={AccountId}",
                        messageId, message.AccountId);
                }

                await Task.Delay(_options.PublishIntervalMs, stoppingToken);
            }

            _logger.LogInformation("Batch complete. Waiting 30s before next batch...");
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }

        _logger.LogInformation("XmlProducer stopped.");
    }

    private static List<(TransactionMessage Message, string MessageId)> BuildDemoBatch()
    {
        var now = DateTime.UtcNow;
        var date = now.ToString("yyyyMMdd");

        return
        [
            Build("1001", "CUST-A001", $"TXN-{date}-0001", 1500.00m, "PAYMENT",    "REF-0001", $"CORR-1001-{now.Ticks:X}", now),
            Build("1001", "CUST-A001", $"TXN-{date}-0002", 2300.50m, "TRANSFER",   "REF-0002", $"CORR-1001-{now.AddSeconds(1).Ticks:X}", now.AddSeconds(1)),
            Build("1001", "CUST-A001", $"TXN-{date}-0003",  875.00m, "PAYMENT",    "REF-0003", $"CORR-1001-{now.AddSeconds(2).Ticks:X}", now.AddSeconds(2)),
            Build("2001", "CUST-B002", $"TXN-{date}-0004", 4200.00m, "SETTLEMENT", "REF-0004", $"CORR-2001-{now.AddSeconds(3).Ticks:X}", now.AddSeconds(3)),
            Build("2001", "CUST-B002", $"TXN-{date}-0005",  690.25m, "PAYMENT",    "REF-0005", $"CORR-2001-{now.AddSeconds(4).Ticks:X}", now.AddSeconds(4)),
            Build("3001", "CUST-C003", $"TXN-{date}-0006",12000.00m, "WIRE",       "REF-0006", $"CORR-3001-{now.AddSeconds(5).Ticks:X}", now.AddSeconds(5)),
            Build("4001", "CUST-D004", $"TXN-{date}-0007",  350.75m, "PAYMENT",    "REF-0007", $"CORR-4001-{now.AddSeconds(6).Ticks:X}", now.AddSeconds(6)),
        ];
    }

    private static (TransactionMessage, string) Build(
        string accountId, string customerId, string transactionId,
        decimal amount, string operation, string refNum, string correlationId, DateTime ts)
    {
        var messageId = Guid.NewGuid().ToString("N")[..12].ToUpper();
        return (new TransactionMessage
        {
            AccountId = accountId,
            CustomerId = customerId,
            TransactionId = transactionId,
            Amount = amount,
            Currency = "USD",
            Timestamp = ts,
            Operation = operation,
            ReferenceNumber = refNum,
            CorrelationId = correlationId,
            Status = "PENDING"
        }, messageId);
    }
}
