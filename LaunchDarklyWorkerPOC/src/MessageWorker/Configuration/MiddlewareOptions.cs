using System.ComponentModel.DataAnnotations;

namespace LaunchDarklyPOC.MessageWorker.Configuration;

public sealed class MiddlewareOptions
{
    public const string SectionName = "Middleware";

    public EndpointOptions Python { get; set; } = new();
    public EndpointOptions DotNet { get; set; } = new();
}

public sealed class EndpointOptions
{
    [Required]
    public string BaseUrl { get; set; } = string.Empty;

    [Range(5, 300)]
    public int TimeoutSeconds { get; set; } = 30;

    [Range(0, 10)]
    public int RetryCount { get; set; } = 3;

    [Range(100, 30000)]
    public int RetryDelayMs { get; set; } = 500;
}
