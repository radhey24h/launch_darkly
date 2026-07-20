namespace LaunchDarklyConsolePOC.Models;

/// <summary>
/// Aggregate statistics computed after all orders have been processed.
/// Displayed as the final dashboard at the end of the run.
/// </summary>
public sealed class ProcessingSummary
{
    public int TotalOrders { get; init; }
    public int UniqueAccounts { get; init; }

    /// <summary>Number of distinct AccountIds that appeared more than once.</summary>
    public int RepeatedAccounts { get; init; }

    public int PythonCount { get; init; }
    public int DotNetCount { get; init; }

    /// <summary>Python routing percentage (0–100).</summary>
    public double PythonPercent { get; init; }

    /// <summary>DotNet routing percentage (0–100).</summary>
    public double DotNetPercent { get; init; }

    public TimeSpan ExecutionTime { get; init; }

    /// <summary>True when every repeated AccountId received the same variation on every call.</summary>
    public bool IsConsistent { get; init; }
}
