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

        // Tracks the first-seen variation for each AccountId so we can annotate
        // subsequent occurrences and prove deterministic routing in real time.
        // key = AccountId, value = (firstVariation, occurrencesSoFar)
        var seen = new Dictionary<string, (string Variation, int Count)>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < orders.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var order     = orders[i];
            var variation = _routing.EvaluateVariation(order.AccountId);

            // Determine whether this account has been seen before.
            bool isRepeat = seen.TryGetValue(order.AccountId, out var prior);
            int occurrenceNumber;

            if (isRepeat)
            {
                occurrenceNumber = prior.Count + 1;
                seen[order.AccountId] = (prior.Variation, occurrenceNumber);
            }
            else
            {
                occurrenceNumber = 1;
                seen[order.AccountId] = (variation, 1);
            }

            // Resolve the destination service by variation name (case-insensitive).
            // Falls back to the first registered service (Python) when the variation
            // is unrecognised — matches the DefaultVariation safety net.
            var destination = _destinations.FirstOrDefault(d =>
                string.Equals(d.VariationName, variation, StringComparison.OrdinalIgnoreCase))
                ?? _destinations.First();

            if (verbose)
            {
                // destination.Process prints the 3-line order block (no trailing blank line).
                destination.Process(order);

                // If this account has appeared before, annotate immediately below the
                // order block so the viewer can see deterministic routing in action.
                if (isRepeat)
                {
                    // Consistent: same variation as first occurrence — expected behaviour.
                    // Inconsistent: should never happen with LaunchDarkly; shown for clarity.
                    bool consistent = string.Equals(variation, prior.Variation, StringComparison.OrdinalIgnoreCase);

                    Console.ForegroundColor = consistent ? ConsoleColor.Cyan : ConsoleColor.Red;
                    Console.WriteLine(consistent
                        ? $"  ↩ Repeat #{occurrenceNumber} of {order.AccountId} — CONSISTENT ✓  (same as occurrence #1)"
                        : $"  ↩ Repeat #{occurrenceNumber} of {order.AccountId} — MISMATCH ✗  (was {prior.Variation}, now {variation})");
                    Console.ResetColor();
                }

                Console.WriteLine(); // blank separator between order blocks
            }
            else
            {
                // Compact single-line format for high-volume performance runs.
                var color = variation.Equals("python", StringComparison.OrdinalIgnoreCase)
                    ? ConsoleColor.Yellow
                    : ConsoleColor.Green;

                Console.ForegroundColor = color;

                if (isRepeat)
                    Console.Write($"  {order.OrderId}  {order.AccountId,-15}  {variation.ToUpperInvariant()}");
                else
                    Console.Write($"  {order.OrderId}  {order.AccountId,-15}  {variation.ToUpperInvariant()}");

                Console.ResetColor();

                if (isRepeat)
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.Write($"  ↩ #{occurrenceNumber}");
                    Console.ResetColor();
                }

                Console.WriteLine();

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
