using LaunchDarklyConsolePOC.Models;

namespace LaunchDarklyConsolePOC.Generator;

/// <summary>
/// Generates a list of mock <see cref="Order"/> objects for demonstration purposes.
/// The generator deliberately introduces repeated AccountIds so that the deterministic
/// routing validation step has observable data to check.
/// </summary>
public interface IOrderGenerator
{
    /// <summary>
    /// Generates exactly <paramref name="count"/> mock orders.
    /// The returned collection is deterministic for a given <paramref name="count"/>
    /// (fixed random seed) so every demo run with the same count produces the same orders.
    /// </summary>
    IReadOnlyList<Order> Generate(int count);
}
