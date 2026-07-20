using LaunchDarklyConsolePOC.Interfaces;
using LaunchDarklyConsolePOC.Models;

namespace LaunchDarklyConsolePOC.Services;

/// <summary>
/// Simulates routing an order to the Python (legacy TIBCO mock) backend.
/// In a real system this would publish to a queue or invoke an HTTP endpoint.
/// Here it writes the routing decision to the console in the required format.
/// </summary>
public sealed class PythonDestinationService : IDestinationService
{
    /// <inheritdoc />
    public string VariationName => "python";

    /// <inheritdoc />
    public void Process(Order order)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"  OrderId     : {order.OrderId}");
        Console.WriteLine($"  Account     : {order.AccountId}");
        Console.WriteLine($"  Destination : Python");
        Console.ResetColor();
    }
}
