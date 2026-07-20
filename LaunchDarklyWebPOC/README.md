# LaunchDarkly Canary Deployment POC

A production-quality proof-of-concept demonstrating **progressive traffic rollout** (canary deployment) using LaunchDarkly feature flags.

Our organization has an existing **TIBCO** implementation serving 100% of production traffic. A new **.NET Core** implementation has been developed. Rather than switching everyone at once, we use LaunchDarkly to route traffic gradually — starting at 10% to the new service — while monitoring for issues. The Python FastAPI service in this POC mocks the TIBCO legacy system.

> **Key insight**: LaunchDarkly never routes HTTP traffic itself. It only tells your application *which backend to call*. All routing logic lives in the ASP.NET Core Gateway.

---

## Architecture

```
                        Client
                           │
                     HTTP Request
                           │
                           ▼
              ┌────────────────────────────┐
              │    ASP.NET Core Gateway    │  :5000
              │                            │
              │  CorrelationIdMiddleware   │
              │  RequestLoggingMiddleware  │
              │                            │
              │  LaunchDarkly SDK          │◄── streams flags from
              │  (in-memory flag store)    │    app.launchdarkly.com
              │                            │
              │  Evaluate: backend-routing │
              │  Context key = userId      │
              └─────────────┬──────────────┘
                             │
              Deterministic Hash(flagKey + userId)
                             │
              ┌──────────────┴──────────────┐
              │  bucket < 10% ?             │
              │                             │
         YES  ▼                       NO   ▼
  ┌──────────────────┐      ┌─────────────────────────┐
  │  .NET Backend    │      │  Python FastAPI Backend  │
  │  (new service)   │      │  (mock TIBCO legacy)     │
  │  Port: 5001      │      │  Port: 8000              │
  │  10% traffic     │      │  90% traffic             │
  └──────────────────┘      └─────────────────────────┘
```

### How It Works Internally

When a request reaches the Gateway, the LaunchDarkly SDK evaluates the `backend-routing` flag **in microseconds from an in-memory cache** (the SDK streams updates from LaunchDarkly continuously). It uses a **deterministic hash** of `(flagKey + userId)` to place the user into a bucket 0–9999:

```
bucket = murmurhash3("backend-routing" + "." + userId) % 10000
```

- `bucket < 1000` (10%) → routes to `.NET backend`
- `bucket ≥ 1000` (90%) → routes to `Python backend`

Because the hash is deterministic, **the same user always gets the same backend** (sticky routing). Customer A might always hash to bucket 42 (dotnet), Customer B to bucket 7823 (python). They never switch mid-session.

### Progressive Rollout Stages

As confidence in the new .NET backend grows, you simply update the percentage in the LaunchDarkly dashboard — **no code changes, no redeployment**:

| Stage | .NET | Python (TIBCO) | When to advance |
|-------|------|----------------|-----------------|
| Initial | 10% | 90% | Start here — baseline stability |
| Phase 2 | 25% | 75% | After 24 h with no regressions |
| Phase 3 | 50% | 50% | After 48 h stable |
| Phase 4 | 75% | 25% | After 1 week stable |
| Complete | 100% | 0% | Migration done, decommission TIBCO |

If any issue occurs, set `.NET = 0%` in the dashboard for an **instant rollback** with no deployment.

### Key Design Decisions

| Decision | Rationale |
|----------|-----------|
| **Gateway evaluates flags** | LaunchDarkly does NOT route HTTP traffic. Your code does. |
| **Stable `userId` as context key** | Same user always hits same backend. Never use `Guid.NewGuid()`. |
| **Singleton `LaunchDarklyService`** | SDK maintains a streaming connection + in-memory flag store. Only one instance per process. |
| **Typed `HttpClient`** | `HttpClientFactory` manages connection pooling — avoids socket exhaustion from `new HttpClient()`. |
| **Python as fallback default** | If LaunchDarkly is unreachable, traffic defaults to the known-stable legacy backend. |

### Why Enterprises Use This Pattern

- **No redeployment** to change rollout percentages.
- **Stable UX** through deterministic hashing — users don't flip between systems.
- **Instant rollback** — one dashboard change, picked up within ~30 seconds.
- **Targeted rollout** — can target specific tenants/customers before full rollout.

---

## Project Structure

```
LaunchDarklyCanaryPOC/
├── src/
│   ├── Gateway/                           # Entry point — evaluates LD flags, routes traffic
│   │   ├── Controllers/
│   │   │   └── OrdersController.cs        # GET /api/orders/{userId}
│   │   ├── Services/
│   │   │   ├── ILaunchDarklyService.cs    # Interface — mockable in unit tests
│   │   │   ├── LaunchDarklyService.cs     # Singleton SDK wrapper
│   │   │   ├── IBackendRouter.cs          # Interface — mockable in unit tests
│   │   │   └── BackendRouter.cs           # Core flag evaluation + routing logic
│   │   ├── Clients/
│   │   │   ├── IDotNetBackendClient.cs    # Interface
│   │   │   ├── DotNetBackendClient.cs     # Typed HttpClient via HttpClientFactory
│   │   │   ├── IPythonBackendClient.cs    # Interface
│   │   │   └── PythonBackendClient.cs     # Typed HttpClient via HttpClientFactory
│   │   ├── Middleware/
│   │   │   ├── CorrelationIdMiddleware.cs  # X-Correlation-Id header propagation
│   │   │   └── RequestLoggingMiddleware.cs # Structured request/response logging
│   │   ├── Models/
│   │   │   └── OrderResponse.cs           # Shared response models
│   │   ├── Options/
│   │   │   ├── LaunchDarklyOptions.cs     # SDK key + flag key (Options pattern)
│   │   │   └── BackendOptions.cs          # Backend URLs (Options pattern)
│   │   ├── Dockerfile                     # Multi-stage Docker build
│   │   ├── Program.cs                     # DI, middleware pipeline, health checks
│   │   └── appsettings.json               # ← Set LaunchDarkly:SdkKey here
│   │
│   ├── DotNetBackend/                     # New .NET service (receives 10% traffic)
│   │   ├── Dockerfile
│   │   ├── Program.cs                     # Minimal API — GET /api/orders/{userId}
│   │   └── appsettings.json
│   │
│   └── PythonBackend/                     # Mock TIBCO legacy system (receives 90% traffic)
│       ├── Dockerfile
│       ├── main.py                        # FastAPI app — GET /api/orders/{user_id}
│       └── requirements.txt
│
├── tests/
│   └── Gateway.Tests/
│       ├── BackendRouterTests.cs          # 8 unit tests — mocked LD + backends
│       └── Gateway.Tests.csproj
│
├── postman/
│   └── LaunchDarklyCanaryPOC.postman_collection.json
│
├── .vscode/
│   ├── launch.json                        # Compound debug config (F5 = all 3 services)
│   └── tasks.json                         # Build / run / test tasks
│
├── docker-compose.yml                     # Run all 3 services with Docker
├── LaunchDarklyCanaryPOC.sln
├── LaunchDarkly-Setup.md                  # Step-by-step LaunchDarkly flag setup guide
└── README.md                              # This file
```

---

## Prerequisites

| Tool | Version | Purpose |
|------|---------|---------|
| [.NET SDK](https://dotnet.microsoft.com/download) | 10.0+ | Gateway + .NET Backend |
| [Python](https://www.python.org/downloads/) | 3.12+ | Python Backend |
| [VS Code](https://code.visualstudio.com/) | Latest | IDE with debugging |
| [C# Extension](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csharp) | Latest | .NET debugging in VS Code |
| [Python Extension](https://marketplace.visualstudio.com/items?itemName=ms-python.python) | Latest | Python debugging in VS Code |
| [Docker Desktop](https://www.docker.com/products/docker-desktop/) | Latest | Container deployment (optional) |
| [LaunchDarkly Account](https://app.launchdarkly.com) | Free tier OK | Feature flag service |

---

## LaunchDarkly Setup (Required First)

You must create the feature flag in LaunchDarkly before running the solution. See [`LaunchDarkly-Setup.md`](LaunchDarkly-Setup.md) for the full step-by-step guide with screenshots.

### Quick summary

1. Sign up at [app.launchdarkly.com](https://app.launchdarkly.com) (free)
2. Create a project → get a **Server-side SDK key** (starts with `sdk-`)
3. Create a feature flag:

| Field | Value |
|-------|-------|
| **Key** | `backend-routing` ← must match exactly |
| **Type** | String |
| **Variation A value** | `dotnet` |
| **Variation B value** | `python` |

4. Set the rollout: **10% `dotnet` / 90% `python`**
5. Toggle the flag **ON**
6. Paste your SDK key into `src/Gateway/appsettings.json`:

```json
{
  "LaunchDarkly": {
    "SdkKey": "sdk-YOUR-ACTUAL-KEY-HERE"
  }
}
```

> ⚠️ Use the **Server-side** SDK key (starts with `sdk-`), never the Client-side ID.  
> Never commit the real key to source control — use environment variables in production.

---

## Running Locally

### Option A — VS Code Debugger (Recommended)

1. Open the `LaunchDarklyCanaryPOC` folder in VS Code
2. Set your SDK key in `src/Gateway/appsettings.json`
3. Install Python dependencies: `Ctrl+Shift+P` → **Run Task** → `pip-install`
4. Open **Run & Debug** panel (`Ctrl+Shift+D`)
5. Select **"🚀 All Services"** → press **F5**

Three integrated terminals open simultaneously. Swagger UIs launch automatically.

---

### Option B — Three Terminals

**Terminal 1 — Python Backend (port 8000)**
```powershell
cd src\PythonBackend
pip install -r requirements.txt
python main.py
```

**Terminal 2 — .NET Backend (port 5001)**
```powershell
cd src\DotNetBackend
dotnet run
```

**Terminal 3 — Gateway (port 5000)**
```powershell
cd src\Gateway
$env:LaunchDarkly__SdkKey = "sdk-your-key-here"
dotnet run
```

---

## Running with Docker

```powershell
# 1. Set your SDK key
$env:LAUNCHDARKLY_SDK_KEY = "sdk-your-key-here"

# 2. Build and start all 3 services
docker-compose up --build

# 3. Tear down
docker-compose down
```

Docker build context is the solution root; each service's `Dockerfile` lives inside its own folder (`src/Gateway/Dockerfile`, etc.).

---

## Swagger UIs

| Service | URL | Description |
|---------|-----|-------------|
| Gateway | http://localhost:5000/swagger | Main entry point with routing metadata |
| .NET Backend | http://localhost:5001/swagger | New .NET service (10% traffic) |
| Python Backend | http://localhost:8000/swagger | Mock TIBCO legacy (90% traffic) |

---

## Testing

### Health Checks

```powershell
curl http://localhost:5000/health   # includes LaunchDarkly SDK status
curl http://localhost:5001/health
curl http://localhost:8000/health
```

### Sample Requests

```powershell
# Route an order via the Gateway
curl http://localhost:5000/api/orders/alice

# Test multiple users — observe sticky routing
foreach ($user in @("alice", "bob", "charlie", "david", "emma")) {
    $result = Invoke-RestMethod "http://localhost:5000/api/orders/$user"
    Write-Host "$user -> $($result.selectedVariation)"
}
```

At 10% rollout, most users return `"python"`. The same user always returns the same variation.

### Unit Tests

```powershell
cd tests\Gateway.Tests
dotnet test --verbosity normal
```

**8 tests, all passing:**

| Test | What it verifies |
|------|-----------------|
| `WhenVariationIsDotNet_*` | `"dotnet"` variation calls .NET client, never Python |
| `WhenVariationIsPython_*` | `"python"` variation calls Python client, never .NET |
| `WhenVariationIsUnknown_*` (×3) | Unknown variation falls back safely to Python |
| `ShouldMeasureElapsedTime` | Response includes non-negative `elapsedMs` |
| `SameUserId_AlwaysPassesSameKey` | Router always passes identical userId to LaunchDarkly (enables sticky routing) |
| `WhenCancelled_*` | `TaskCanceledException` propagates correctly |

---

## Expected Response Format

### Gateway Response (enriched with routing metadata)

```json
{
  "data": {
    "backend": "python",
    "orderId": 1234,
    "customer": "ALICE",
    "message": "Response from Python Backend (Mock TIBCO)",
    "timestamp": "2026-07-11T05:20:00.000Z",
    "userId": "alice"
  },
  "selectedVariation": "python",
  "elapsedMs": 12,
  "correlationId": "a1b2c3d4e5f6..."
}
```

The `selectedVariation` field confirms which LaunchDarkly bucket the user landed in.  
The `correlationId` traces the request across Gateway → Backend → logs.

---

## Sticky Routing — How Deterministic Hashing Works

LaunchDarkly computes:
```
bucket = murmurhash3("backend-routing" + "." + userId) % 10000
```

| userId | Approximate bucket | Variation at 10% rollout |
|--------|-------------------|--------------------------|
| alice | ~5300 | `python` (bucket ≥ 1000) |
| bob | ~7800 | `python` |
| charlie | ~4100 | `python` |
| david | ~6500 | `python` |
| emma | ~2900 | `python` |

Users with `bucket < 1000` get `dotnet`. Since the hash is stable, the same user is **always** in the same bucket — they never flip between backends mid-session.

> **Important**: Always pass a stable identifier (`userId`, `customerId`, `tenantId`) as the LaunchDarkly context key.  
> **Never** use `Guid.NewGuid()` — a new GUID every request means a different bucket every request, destroying sticky routing.

### Code example (from `LaunchDarklyService.cs`)

```csharp
// Correct — stable key, deterministic bucket assignment
var context = Context.Builder(userId).Build();

// WRONG — new GUID every call = random backend every call
// var context = Context.Builder(Guid.NewGuid().ToString()).Build();
```

---

## Adjusting the Rollout

All changes happen in the **LaunchDarkly dashboard** — no code changes, no deployments, no restarts:

| Goal | Action in Dashboard |
|------|---------------------|
| Expand to 25% .NET | Set `dotnet` → 25%, `python` → 75% → Save |
| Rollback to 0% .NET | Set `dotnet` → 0%, `python` → 100% → Save |
| Full migration | Set `dotnet` → 100%, `python` → 0% → Save |
| Emergency kill switch | Toggle flag **OFF** → all traffic uses default (`python`) |

The Gateway SDK receives the new configuration within ~30 seconds via its persistent streaming connection to LaunchDarkly.

---

## Debugging in VS Code

Set breakpoints at these locations to trace an entire request:

| File | Line area | What you'll see |
|------|-----------|-----------------|
| `src/Gateway/Controllers/OrdersController.cs` | ~L80 | Request arrives, correlation ID extracted |
| `src/Gateway/Services/BackendRouter.cs` | ~L45 | LaunchDarkly variation evaluated |
| `src/Gateway/Services/LaunchDarklyService.cs` | ~L85 | `GetBackendVariation()` — context built, flag evaluated |
| `src/Gateway/Clients/DotNetBackendClient.cs` | ~L32 | HTTP call to .NET backend |
| `src/DotNetBackend/Program.cs` | ~L35 | .NET backend receives request |
| `src/PythonBackend/main.py` | ~L68 | Python backend receives request |

### Log output (Development mode)

```
[abc123] → GET /api/orders/alice
LaunchDarkly evaluated flag 'backend-routing' for user 'alice': variation = 'python'
Routing decision: UserId=alice → Variation=python
Forwarding to Python backend (mock TIBCO) for UserId=alice
Request completed: UserId=alice, Backend=python, ElapsedMs=15
[abc123] ← 200 GET /api/orders/alice (16ms)
```

---

## Postman Collection

Import `postman/LaunchDarklyCanaryPOC.postman_collection.json` into Postman.

Includes:
- Health checks for all 3 services
- Order requests for alice, bob, charlie, david, emma
- Sticky routing verification (stores first variation, asserts same on repeat)
- Direct backend calls (bypass Gateway — useful for backend isolation testing)
- Version endpoint calls

---

## Troubleshooting

### "LaunchDarkly SDK did not initialize"
- Verify `SdkKey` is set correctly (environment variable overrides appsettings)
- Check outbound HTTPS to `stream.launchdarkly.com` (port 443) is not blocked
- The Gateway will still work using the `DefaultVariation` (`python`) as fallback

### "Flag always returns 'python'"
- Verify the flag is **turned ON** in the correct environment
- With 10% rollout, most users get `python` — this is expected behaviour
- Try userIds like `user001`–`user020` to find ones in the dotnet bucket
- Use the LaunchDarkly **Live Events** debugger to confirm evaluations are reaching LD

### "502 from Gateway"
- Verify both backend services are running before starting the Gateway
- Check backend URLs in `appsettings.json` match the running ports
- In Docker, container names are used as hostnames (configured in `docker-compose.yml`)
