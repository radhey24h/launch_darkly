namespace LaunchDarklyConsolePOC.Models;

/// <summary>
/// The outcome of routing a single order through the LaunchDarkly evaluation engine.
/// Stored so the validation step can check that every order for a given AccountId
/// always received the same variation.
/// </summary>
public sealed class ProcessingResult
{
    /// <summary>The order that was processed.</summary>
    public required Order Order { get; init; }

    /// <summary>
    /// The LaunchDarkly variation that was returned for this order.
    /// Expected values: "dotnet" | "python"  (matches the flag variation names).
    /// </summary>
    public required string Variation { get; init; }
}
