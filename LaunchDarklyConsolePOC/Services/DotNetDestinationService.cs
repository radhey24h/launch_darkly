using LaunchDarklyConsolePOC.Interfaces;
using LaunchDarklyConsolePOC.Models;

namespace LaunchDarklyConsolePOC.Services;

/// <summary>
/// Simulates routing an order to the new ASP.NET Core backend.
/// In a real system this would publish to a queue or invoke an HTTP endpoint.
/// Here it writes the routing decision to the console in the required format.
/// </summary>
public sealed class DotNetDestinationService : IDestinationService
{
    /// <inheritdoc />
    public string VariationName => "dotnet";

    /// <inheritdoc />
    public void Process(Order order)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  OrderId     : {order.OrderId}");
        Console.WriteLine($"  Account     : {order.AccountId}");
        Console.WriteLine($"  Destination : DotNet");
        Console.ResetColor();
    }
}
