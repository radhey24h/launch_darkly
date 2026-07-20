using System.Xml.Serialization;

namespace LaunchDarklyPOC.Shared.Models;

[XmlRoot("TransactionMessage")]
public class TransactionMessage
{
    [XmlElement("AccountId")]
    public string AccountId { get; set; } = string.Empty;

    [XmlElement("CustomerId")]
    public string CustomerId { get; set; } = string.Empty;

    [XmlElement("TransactionId")]
    public string TransactionId { get; set; } = string.Empty;

    [XmlElement("Amount")]
    public decimal Amount { get; set; }

    [XmlElement("Currency")]
    public string Currency { get; set; } = "USD";

    [XmlElement("Timestamp")]
    public DateTime Timestamp { get; set; }

    [XmlElement("Operation")]
    public string Operation { get; set; } = string.Empty;

    [XmlElement("ReferenceNumber")]
    public string ReferenceNumber { get; set; } = string.Empty;

    [XmlElement("CorrelationId")]
    public string CorrelationId { get; set; } = string.Empty;

    [XmlElement("Status")]
    public string Status { get; set; } = "PENDING";
}
