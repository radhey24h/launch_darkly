using LaunchDarklyConsolePOC.Models;

namespace LaunchDarklyConsolePOC.Generator;

/// <summary>
/// Generates a realistic set of mock orders with a deliberately skewed account
/// distribution so that the deterministic-routing validation step has a meaningful
/// dataset to verify against.
///
/// Account frequency tiers:
///   Tier 1 — 5  accounts × weight 30  → each appears ~33 times in 1 000-order run
///   Tier 2 — 10 accounts × weight 15  → each appears ~17 times
///   Tier 3 — 20 accounts × weight  5  → each appears ~6  times
///   Tier 4 — 50 accounts × weight  2  → each appears ~2  times
///   Tier 5 — 400 single-use accounts  → most appear 0–1 times
///
/// The generator uses a fixed seed (42) so every run with the same count produces
/// exactly the same orders — making demos fully reproducible.
/// </summary>
public sealed class OrderGenerator : IOrderGenerator
{
    // ── Static account pool built once at class load ───────────────────────
    private static readonly string[] AccountPool = BuildAccountPool();

    private static string[] BuildAccountPool()
    {
        var pool = new List<string>(900);

        // Tier 1: very high frequency (30 slots each)
        for (int i = 1; i <= 5; i++)
            for (int j = 0; j < 30; j++)
                pool.Add($"Account{i:D3}");

        // Tier 2: high frequency (15 slots each)
        for (int i = 6; i <= 15; i++)
            for (int j = 0; j < 15; j++)
                pool.Add($"Account{i:D3}");

        // Tier 3: medium frequency (5 slots each)
        for (int i = 16; i <= 35; i++)
            for (int j = 0; j < 5; j++)
                pool.Add($"Account{i:D3}");

        // Tier 4: low frequency (2 slots each)
        for (int i = 36; i <= 85; i++)
            for (int j = 0; j < 2; j++)
                pool.Add($"Account{i:D3}");

        // Tier 5: single occurrence (1 slot each)
        for (int i = 86; i <= 485; i++)
            pool.Add($"Account{i:D3}");

        return pool.ToArray();
    }

    // ── Realistic-looking demo data ────────────────────────────────────────
    private static readonly string[] CustomerNames =
    [
        "Alice Johnson",   "Bob Smith",       "Carol Williams",  "David Brown",
        "Emma Davis",      "Frank Wilson",    "Grace Martinez",  "Henry Anderson",
        "Isabella Thomas", "James Jackson",   "Karen White",     "Liam Harris",
        "Mia Thompson",    "Noah Garcia",     "Olivia Martinez", "Peter Robinson",
        "Quinn Clark",     "Rachel Lewis",    "Samuel Lee",      "Tina Walker",
        "Uma Hall",        "Victor Allen",    "Wendy Young",     "Xavier Hernandez",
        "Yolanda King",    "Zachary Wright",  "Amy Scott",       "Brian Torres",
        "Chloe Nguyen",    "Derek Adams"
    ];

    private static readonly string[] Currencies = ["USD", "EUR", "GBP", "JPY", "INR", "AUD", "CAD"];

    // ── IOrderGenerator implementation ────────────────────────────────────
    public IReadOnlyList<Order> Generate(int count)
    {
        // Fixed seed ensures identical output for the same count across runs.
        var rng = new Random(42);
        var orders = new List<Order>(count);

        for (int i = 0; i < count; i++)
        {
            orders.Add(new Order
            {
                OrderId      = $"ORD-{i + 1:D6}",
                AccountId    = AccountPool[rng.Next(AccountPool.Length)],
                CustomerName = CustomerNames[rng.Next(CustomerNames.Length)],
                Amount       = Math.Round((decimal)(rng.NextDouble() * 9_999 + 1), 2),
                Currency     = Currencies[rng.Next(Currencies.Length)],
                Timestamp    = DateTimeOffset.UtcNow.AddSeconds(-rng.Next(0, 86_400))
            });
        }

        return orders;
    }
}
