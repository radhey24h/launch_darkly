using LaunchDarklyConsolePOC.Interfaces;
using LaunchDarklyConsolePOC.Models;
using LaunchDarklyConsolePOC.Routing;
using Microsoft.Extensions.Logging;

namespace LaunchDarklyConsolePOC.Services;

/// <summary>
/// Core processing pipeline.
///
/// For each order:
///   1. Evaluates the LaunchDarkly feature flag using the order's AccountId as
///      the evaluation context key.
///   2. Resolves the matching <see cref="IDestinationService"/> by variation name.
///   3. Calls Process() — which prints the routing decision to the console.
///   4. Records the result for post-run validation and statistics.
///
/// Verbose output (3-line per-order format) is shown for runs of ≤ 1 000 orders.
/// For larger runs a single-line compact format is used to keep the terminal readable
/// during performance/throughput testing.
/// </summary>
public sealed class OrderProcessingService : IOrderProcessor
{
    private readonly IRoutingService _routing;
    private readonly IEnumerable<IDestinationService> _destinations;
    private readonly ILogger<OrderProcessingService> _logger;

    // Threshold above which the compact (single-line) output format is used.
    private const int VerboseThreshold = 1_000;

    public OrderProcessingService(
        IRoutingService routing,
        IEnumerable<IDestinationService> destinations,
        ILogger<OrderProcessingService> logger)
    {
        _routing      = routing;
        _destinations = destinations;
        _logger       = logger;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ProcessingResult>> ProcessAsync(
        IReadOnlyList<Order> orders,
        CancellationToken cancellationToken = default)
    {
        var results  = new List<ProcessingResult>(orders.Count);
        var verbose  = orders.Count <= VerboseThreshold;
        var progress = orders.Count / 10; // emit a progress line every 10 % in compact mode

        for (int i = 0; i < orders.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var order     = orders[i];
            var variation = _routing.EvaluateVariation(order.AccountId);

            // Resolve the destination service by variation name (case-insensitive).
            // Falls back to the first registered service (Python) when the variation
            // is unrecognised — matches the DefaultVariation safety net.
            var destination = _destinations.FirstOrDefault(d =>
                string.Equals(d.VariationName, variation, StringComparison.OrdinalIgnoreCase))
                ?? _destinations.First();

            if (verbose)
            {
                // Full 3-line format as specified in requirements
                destination.Process(order);
            }
            else
            {
                // Compact single-line format for high-volume performance runs
                var color = variation.Equals("python", StringComparison.OrdinalIgnoreCase)
                    ? ConsoleColor.Yellow
                    : ConsoleColor.Green;

                Console.ForegroundColor = color;
                Console.WriteLine($"  {order.OrderId}  {order.AccountId,-15}  {variation.ToUpperInvariant()}");
                Console.ResetColor();

                // Progress indicator every 10 %
                if (progress > 0 && (i + 1) % progress == 0)
                {
                    double pct = (i + 1.0) / orders.Count * 100;
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"  ── {i + 1:N0} / {orders.Count:N0} processed ({pct:F0}%) ──");
                    Console.ResetColor();
                }
            }

            results.Add(new ProcessingResult { Order = order, Variation = variation });
        }

        _logger.LogDebug(
            "Processed {Count} orders. Verbose={Verbose}",
            orders.Count, verbose);

        return Task.FromResult<IReadOnlyList<ProcessingResult>>(results);
    }
}
