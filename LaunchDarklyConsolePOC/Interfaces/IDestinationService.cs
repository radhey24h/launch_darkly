using LaunchDarklyConsolePOC.Models;

namespace LaunchDarklyConsolePOC.Interfaces;

/// <summary>
/// Represents a routing destination (DotNet backend or Python backend).
/// Each implementation writes the routing decision to the console so that
/// the demo makes the traffic split visually obvious without requiring
/// any running HTTP services.
/// </summary>
public interface IDestinationService
{
    /// <summary>
    /// The LaunchDarkly variation name this service handles.
    /// Must be "dotnet" or "python" (case-insensitive comparison is used when selecting).
    /// </summary>
    string VariationName { get; }

    /// <summary>
    /// "Processes" the order by printing the routing decision to the console.
    /// In a real system this would forward the order to the appropriate microservice.
    /// </summary>
    void Process(Order order);
}
