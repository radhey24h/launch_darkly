using System.ComponentModel.DataAnnotations;

namespace LaunchDarklyPOC.XmlProducer.Configuration;

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

    [Range(500, 60000)]
    public int PublishIntervalMs { get; set; } = 2000;

    [Range(1, 20)]
    public int MaxConnectionRetries { get; set; } = 10;
}
