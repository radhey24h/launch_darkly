using System.ComponentModel.DataAnnotations;

namespace LaunchDarklyPOC.MessageWorker.Configuration;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    [Required]
    public string Host { get; set; } = "localhost";

    [Range(1, 65535)]
    public int Port { get; set; } = 5672;

    [Required]
    public string Username { get; set; } = "guest";

    [Required]
    public string Password { get; set; } = "guest";

    [Required]
    public string QueueName { get; set; } = "middleware-processing";

    [Range(1, 100)]
    public ushort PrefetchCount { get; set; } = 1;

    [Range(1, 30)]
    public int MaxConnectionRetries { get; set; } = 15;
}
