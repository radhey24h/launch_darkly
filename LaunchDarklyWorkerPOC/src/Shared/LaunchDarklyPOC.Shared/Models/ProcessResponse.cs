namespace LaunchDarklyPOC.Shared.Models;

public class ProcessResponse
{
    public string TransactionId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ProcessedBy { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; }
    public string Message { get; set; } = string.Empty;
}
