using System.ComponentModel.DataAnnotations;

namespace LaunchDarklyPOC.MessageWorker.Configuration;

public sealed class LaunchDarklyOptions
{
    public const string SectionName = "LaunchDarkly";

    [Required]
    public string SdkKey { get; set; } = string.Empty;

    [Range(1, 60)]
    public int StartWaitTimeSeconds { get; set; } = 10;
}
