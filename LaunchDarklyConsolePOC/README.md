# LaunchDarklyConsolePOC

A self-contained .NET 8 console application that demonstrates **LaunchDarkly deterministic routing** without any infrastructure dependencies (no Docker, RabbitMQ, HTTP, or background services).

---

## Purpose

Explain LaunchDarkly canary deployments in demos and presentations by showing that the same `AccountId` is **always** routed to the same backend variation — regardless of how many times the flag is evaluated. No services need to be running.

---

## Architecture

```
Program.cs                 ← Host setup, DI resolution, banner + summary output
│
├── Configuration/         ← LaunchDarklyOptions, AppOptions  (Options Pattern)
├── Generator/             ← IOrderGenerator / OrderGenerator (mock data)
├── Interfaces/            ← IDestinationService, IOrderProcessor
├── Models/                ← Order, ProcessingResult, ProcessingSummary, ValidationReport
├── Routing/               ← IRoutingService / LaunchDarklyRoutingService (SDK wrapper)
├── Services/              ← PythonDestinationService, DotNetDestinationService,
│                             OrderProcessingService
├── Validation/            ← IConsistencyValidator / ConsistencyValidator
└── Extensions/            ← ServiceCollectionExtensions (DI wiring)
```

**Dependencies:** `LaunchDarkly.ServerSdk 7.0.0` · `Microsoft.Extensions.Hosting 8.0.1`

---

## Configuration

Edit `appsettings.json` before running:

| Key | Default | Description |
|-----|---------|-------------|
| `LaunchDarkly:SdkKey` | `sdk-YOUR-SDK-KEY-HERE` | Server-Side SDK key from your LD project |
| `LaunchDarkly:FeatureFlagKey` | `backend-routing` | Must return `"dotnet"` or `"python"` |
| `LaunchDarkly:DefaultVariation` | `python` | Fallback when SDK is offline |
| `App:DefaultOrderCount` | `1000` | Orders generated when no CLI arg is given |

> The SDK key from `LaunchDarklyWorkerPOC` can be reused here — the flag key (`backend-routing`) is the same one configured in `LaunchDarklyWebPOC`.

---

## How to Run

```bash
cd LaunchDarklyConsolePOC

# Default — 1 000 orders
dotnet run

# Performance mode — 10 000 orders
dotnet run -- 10000

# Stress mode — 100 000 orders
dotnet run -- 100000
```

Open `LaunchDarklyConsolePOC.sln` in Visual Studio 2022 or Rider to run and debug.

---

## Expected Output

```
  ╔══════════════════════════════════════════════════════════╗
  ║    LaunchDarkly  ·  Deterministic Routing Console POC   ║
  ╚══════════════════════════════════════════════════════════╝

  Feature Flag : backend-routing
  Order Count  : 1,000

  OrderId     : ORD-000001
  Account     : Account003
  Destination : Python

  OrderId     : ORD-000002
  Account     : Account001
  Destination : DotNet
  ...

  ✓ PASS  Account001   DOTNET   ×34 occurrences
  ✓ PASS  Account002   PYTHON   ×31 occurrences
  ...

  ┌────────────────────────────────────────────────────────┐
  │                       Summary                          │
  └────────────────────────────────────────────────────────┘

  Total Orders            1,000
  Unique Accounts           342
  Repeated Accounts         115
  Python Count              523   ( 52.3 %)
  DotNet Count              477   ( 47.7 %)
  Execution Time          1,234 ms

  ✓  PASS  —  Every AccountId received a consistent variation.
```

---

## Deterministic Routing Explained

LaunchDarkly evaluates a feature flag by:

1. Hashing `(context kind + context key)` — here `"account" + AccountId`.
2. Mapping the hash to a value 0–99 999.
3. Checking whether that value falls inside the percentage bucket assigned to each variation.

Because step 1 is a **pure deterministic hash** (no random component), the same `AccountId` will always produce the same hash → same bucket → **same variation**, as long as the flag rules have not changed. This property is what this POC validates automatically after every run.
