// ============================================================================
//  LaunchDarklyConsolePOC — Program.cs
//  Entry point.  Uses Host.CreateDefaultBuilder so DI, IConfiguration, and
//  ILogger are available through the same patterns as a Worker Service, but
//  without any running hosted services, RabbitMQ, Docker, or HTTP infrastructure.
//
//  Usage:
//    dotnet run               → 1 000 orders (from appsettings.json)
//    dotnet run -- 10000      → 10 000 orders
//    dotnet run -- 100000     → 100 000 orders
// ============================================================================

using LaunchDarklyConsolePOC.Configuration;
using LaunchDarklyConsolePOC.Extensions;
using LaunchDarklyConsolePOC.Generator;
using LaunchDarklyConsolePOC.Interfaces;
using LaunchDarklyConsolePOC.Models;
using LaunchDarklyConsolePOC.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Diagnostics;

// ── 1. Build host (DI + IConfiguration + ILogger wired up) ──────────────────

using var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((ctx, svc) =>
    {
        svc.AddLaunchDarklyConsolePOC(ctx.Configuration);
    })
    .Build();

// ── 2. Resolve configuration and print banner ────────────────────────────────

var ldOpts  = host.Services.GetRequiredService<IOptions<LaunchDarklyOptions>>().Value;
var appOpts = host.Services.GetRequiredService<IOptions<AppOptions>>().Value;

// Command-line override: first positional argument is treated as order count.
int orderCount = args.Length > 0 && int.TryParse(args[0], out var parsed) && parsed > 0
    ? parsed
    : appOpts.DefaultOrderCount;

PrintBanner();

Console.WriteLine($"  SDK Key      : {MaskSdkKey(ldOpts.SdkKey)}");
Console.WriteLine($"  Feature Flag : {ldOpts.FeatureFlagKey}");
Console.WriteLine($"  Default Var  : {ldOpts.DefaultVariation}");
Console.WriteLine($"  Order Count  : {orderCount:N0}");
Console.WriteLine();

// ── 3. Generate orders ───────────────────────────────────────────────────────

var generator = host.Services.GetRequiredService<IOrderGenerator>();

Console.Write("  Generating orders... ");
var orders = generator.Generate(orderCount);
WriteColored($"✓  {orderCount:N0} orders ready", ConsoleColor.Green);
Console.WriteLine();

// ── 4. Process orders (LaunchDarkly evaluation + destination routing) ────────

PrintSeparator("Processing Orders");

var processor = host.Services.GetRequiredService<IOrderProcessor>();

var sw = Stopwatch.StartNew();
var results = await processor.ProcessAsync(orders);
sw.Stop();

// ── 5. Consistency validation ────────────────────────────────────────────────

PrintSeparator("Consistency Validation");
Console.WriteLine();

var validator = host.Services.GetRequiredService<IConsistencyValidator>();
var report    = validator.Validate(results);

PrintValidationDetails(report);

// ── 6. Final summary dashboard ───────────────────────────────────────────────

var summary = BuildSummary(results, report, sw.Elapsed, orderCount);
PrintSummary(summary);

// Exit code signals CI / scripting: 0 = PASS, 1 = FAIL
return summary.IsConsistent ? 0 : 1;

// ============================================================================
//  Local helper methods
// ============================================================================

static void PrintBanner()
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine();
    Console.WriteLine("  ╔══════════════════════════════════════════════════════════╗");
    Console.WriteLine("  ║    LaunchDarkly  ·  Deterministic Routing Console POC   ║");
    Console.WriteLine("  ║    Proves consistent flag evaluation without any infra  ║");
    Console.WriteLine("  ╚══════════════════════════════════════════════════════════╝");
    Console.ResetColor();
    Console.WriteLine();
}

static void PrintSeparator(string? title = null)
{
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.DarkGray;
    if (string.IsNullOrWhiteSpace(title))
    {
        Console.WriteLine("  ──────────────────────────────────────────────────────────");
    }
    else
    {
        Console.WriteLine($"  ── {title} ──────────────────────────────────────────");
    }
    Console.ResetColor();
    Console.WriteLine();
}

static void WriteColored(string text, ConsoleColor color)
{
    Console.ForegroundColor = color;
    Console.WriteLine(text);
    Console.ResetColor();
}

static string MaskSdkKey(string key)
{
    if (string.IsNullOrWhiteSpace(key) || key.Contains("YOUR", StringComparison.OrdinalIgnoreCase))
        return "⚠  NOT CONFIGURED — flag evaluations will use DefaultVariation";

    // Show first 8 chars + "..." + last 4 chars for security
    return key.Length > 14 ? $"{key[..8]}...{key[^4..]}" : "[configured]";
}

/// <summary>
/// Prints one line per AccountId that appeared more than once, proving that
/// every repetition received exactly the same variation (or flagging failures).
/// </summary>
static void PrintValidationDetails(ValidationReport report)
{
    var repeated = report.AccountVariations
        .Where(kvp => kvp.Value.Count >= 2)
        .OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
        .ToList();

    if (repeated.Count == 0)
    {
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine("  (No AccountId appeared more than once in this run.)");
        Console.WriteLine("  Tip: increase the order count to observe repeated accounts.");
        Console.ResetColor();
        Console.WriteLine();
        return;
    }

    foreach (var (accountId, variations) in repeated)
    {
        bool consistent  = variations.Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1;
        var  firstVar    = variations[0];
        var  varColor    = firstVar.Equals("python", StringComparison.OrdinalIgnoreCase)
                           ? ConsoleColor.Yellow : ConsoleColor.Green;

        if (consistent)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("  ✓ PASS  ");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write("  ✗ FAIL  ");
        }
        Console.ResetColor();

        Console.Write($"  {accountId,-15}  ");

        Console.ForegroundColor = varColor;
        Console.Write($"{firstVar.ToUpperInvariant(),-8}");
        Console.ResetColor();

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"  ×{variations.Count} occurrences");
        Console.ResetColor();

        // On failure, list every variation received so the user can see the inconsistency.
        if (!consistent)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"    Received: {string.Join(", ", variations)}");
            Console.ResetColor();
        }
    }

    Console.WriteLine();
}

static ProcessingSummary BuildSummary(
    IReadOnlyList<ProcessingResult> results,
    ValidationReport report,
    TimeSpan elapsed,
    int orderCount)
{
    int pythonCount = results.Count(r =>
        r.Variation.Equals("python", StringComparison.OrdinalIgnoreCase));
    int dotNetCount = results.Count(r =>
        r.Variation.Equals("dotnet", StringComparison.OrdinalIgnoreCase));

    int uniqueAccounts   = report.AccountVariations.Count;
    int repeatedAccounts = report.AccountVariations.Count(kvp => kvp.Value.Count > 1);

    double pythonPct = orderCount > 0 ? (double)pythonCount / orderCount * 100 : 0;
    double dotNetPct = orderCount > 0 ? (double)dotNetCount / orderCount * 100 : 0;

    return new ProcessingSummary
    {
        TotalOrders      = orderCount,
        UniqueAccounts   = uniqueAccounts,
        RepeatedAccounts = repeatedAccounts,
        PythonCount      = pythonCount,
        DotNetCount      = dotNetCount,
        PythonPercent    = pythonPct,
        DotNetPercent    = dotNetPct,
        ExecutionTime    = elapsed,
        IsConsistent     = report.IsConsistent
    };
}

static void PrintSummary(ProcessingSummary s)
{
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("  ┌────────────────────────────────────────────────────────┐");
    Console.WriteLine("  │                       Summary                          │");
    Console.WriteLine("  └────────────────────────────────────────────────────────┘");
    Console.ResetColor();
    Console.WriteLine();

    Console.WriteLine($"  {"Total Orders",-22}  {s.TotalOrders,10:N0}");
    Console.WriteLine($"  {"Unique Accounts",-22}  {s.UniqueAccounts,10:N0}");
    Console.WriteLine($"  {"Repeated Accounts",-22}  {s.RepeatedAccounts,10:N0}");
    Console.WriteLine();

    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine($"  {"Python Count",-22}  {s.PythonCount,10:N0}   ({s.PythonPercent,5:F1} %)");
    Console.ResetColor();

    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"  {"DotNet Count",-22}  {s.DotNetCount,10:N0}   ({s.DotNetPercent,5:F1} %)");
    Console.ResetColor();

    Console.WriteLine();
    Console.WriteLine($"  {"Execution Time",-22}  {s.ExecutionTime.TotalMilliseconds,10:N0} ms");
    Console.WriteLine();

    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine("  ──────────────────────────────────────────────────────────");
    Console.ResetColor();

    if (s.IsConsistent)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine();
        Console.WriteLine("  ✓  PASS  —  Every AccountId received a consistent variation.");
        Console.WriteLine("             LaunchDarkly deterministic routing is working correctly.");
    }
    else
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine();
        Console.WriteLine("  ✗  FAIL  —  One or more AccountIds received inconsistent variations.");
        Console.WriteLine("             Review the validation section above for details.");
    }

    Console.ResetColor();
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine("  ──────────────────────────────────────────────────────────");
    Console.ResetColor();
    Console.WriteLine();
}
