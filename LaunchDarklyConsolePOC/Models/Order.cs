namespace LaunchDarklyConsolePOC.Models;

/// <summary>
/// A financial order that will be routed to either the DotNet or Python backend
/// based on a LaunchDarkly feature flag evaluated against AccountId.
/// </summary>
public sealed class Order
{
    /// <summary>Unique order identifier, e.g. "ORD-000001".</summary>
    public required string OrderId { get; init; }

    /// <summary>
    /// The account context key used for LaunchDarkly evaluation.
    /// Identical AccountIds are guaranteed to receive the same variation
    /// (deterministic / consistent hashing inside the SDK).
    /// </summary>
    public required string AccountId { get; init; }

    /// <summary>Display name of the customer, for demo readability.</summary>
    public required string CustomerName { get; init; }

    /// <summary>Order value, two decimal places.</summary>
    public decimal Amount { get; init; }

    /// <summary>ISO 4217 currency code, e.g. "USD".</summary>
    public required string Currency { get; init; }

    /// <summary>UTC creation time of the order.</summary>
    public DateTimeOffset Timestamp { get; init; }
}
