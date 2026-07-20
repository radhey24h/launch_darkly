namespace LaunchDarklyPOC.Shared.Models;

public class ProcessRequest
{
    public string AccountId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string TransactionId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTime Timestamp { get; set; }
    public string Operation { get; set; } = string.Empty;
    public string ReferenceNumber { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string RoutedFrom { get; set; } = "MessageWorker";
}
