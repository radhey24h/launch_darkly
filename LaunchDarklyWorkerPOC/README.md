# LaunchDarkly Middleware Routing POC

Deterministic per-account routing during a TIBCO → .NET middleware migration, controlled by a LaunchDarkly feature flag. The **same `AccountId` always routes to the same backend** — no switching unless the flag configuration changes.

---

## Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (Compose v2)
- A [LaunchDarkly](https://launchdarkly.com) account (free tier is sufficient)

---

## Architecture Digram

                  +----------------------+
                  |   XML Producer       |
                  +----------+-----------+
                             |
                             |
                       RabbitMQ Queue
                             |
                             v
                 +-------------------------+
                 | .NET Worker Service     |
                 |-------------------------|
                 | Read XML                |
                 | Extract AccountId       |
                 | LaunchDarkly Evaluate   |
                 | Routing Decision        |
                 +-----------+-------------+
                             |
             +---------------+---------------+
             |                               |
             |                               |
             v                               v
+---------------------------+      +-------------------------+
| Python FastAPI            |      | .NET Web API           |
| (Existing TIBCO)          |      | (New Middleware)       |
+---------------------------+      +-------------------------+

## Step 1 — Create the Feature Flag

1. Log in to LaunchDarkly → **Feature Flags → Create flag**
2. **Flag key:** `middleware-routing` &nbsp;|&nbsp; **Flag type:** String
3. Add two variations:

   | Name   | Value    |
   |--------|----------|
   | Python | `python` |
   | DotNet | `dotnet` |

4. Set the **Default variation** (served when the flag is OFF) to `python` (100%).
5. Set the **Default rule** (served when the flag is ON) to a **Percentage rollout:**
   - `90%` → `python`
   - `10%` → `dotnet`
6. Turn the flag **ON** and save.

> **Why it's deterministic:** LaunchDarkly uses consistent hashing on the evaluation context key (`AccountId`). The same `AccountId` hashes to the same bucket every time, so it always receives the same variation — until you explicitly change the flag configuration.

---

## Step 2 — Paste the SDK Key into appsettings.json

1. In LaunchDarkly: **Account settings → Projects → your environment → SDK key** (copy it)
2. Open **`src/MessageWorker/appsettings.json`** and replace the placeholder:

```json
"LaunchDarkly": {
  "SdkKey": "sdk-xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
}
```

The SDK key format is `sdk-` followed by a UUID. The `appsettings.json` is baked into the Docker image at build time, so no extra environment variables or secret files are needed.

---

## Step 3 — Run

```bash
docker compose up --build
```

Docker will build all images and start services in dependency order:
**RabbitMQ → PythonMiddleware → DotNetMiddleware → MessageWorker → XmlProducer**

---

## Step 4 — Validate Everything (Checklist)

Work through each checkpoint in order to confirm the full system is running correctly.

---

### Checkpoint 1 — LaunchDarkly Dashboard

Open your flag in the LaunchDarkly dashboard and confirm:

- [ ] Flag key is exactly `middleware-routing`
- [ ] Flag type is **String**
- [ ] Two variations exist with values `python` and `dotnet` (exact lowercase)
- [ ] Default variation (flag OFF) → `python` (100%)
- [ ] Default rule (flag ON) → Percentage rollout: `90% python` / `10% dotnet`
- [ ] Flag is toggled **ON** in your target environment

**How to verify the variation values:** Click the **JSON** tab on the flag page — you should see:

```json
"variations": [
  { "value": "python" },
  { "value": "dotnet" }
]
```

---

### Checkpoint 2 — SDK Key in appsettings.json

Open `src/MessageWorker/appsettings.json` and confirm the `SdkKey` value:

- [ ] Value starts with `sdk-` (not the placeholder text)
- [ ] The key belongs to the **same environment** where the flag is ON

---

### Checkpoint 3 — All Containers are Healthy

After `docker compose up --build`, run:

```bash
docker compose ps
```

Expected — all services should show `healthy` or `running`:

```
NAME                STATUS
rabbitmq            Up (healthy)
python-middleware   Up (healthy)
dotnet-middleware   Up (healthy)
message-worker      Up
xml-producer        Up
```

- [ ] No container is in `Exit` or `Restarting` state

---

### Checkpoint 4 — LaunchDarkly SDK Connected

```bash
docker compose logs message-worker | findstr /I "launchdarkly"
```

> On Linux/Mac use `grep -i "launchdarkly"` instead of `findstr`.

**Good — SDK connected:**
```
LaunchDarkly SDK initialized successfully.
```

**Bad — SDK key wrong or no internet:**
```
LaunchDarkly SDK did not initialize within 10s. Evaluations will use default values.
```

- [ ] Log shows `initialized successfully`

If you see the warning, check: (a) the SDK key is correct, (b) the `message-worker` container has outbound internet access.

---

### Checkpoint 5 — Messages are Routing Deterministically

```bash
docker compose logs message-worker --follow
```

Watch for `Processed` log lines. For each `AccountId`, the `Variation` and `Destination` must be **identical across all messages for that account**:

```
AccountId=1001  Variation=python  Destination=PythonMiddleware  Success=True
AccountId=1001  Variation=python  Destination=PythonMiddleware  Success=True   ← same every time
AccountId=2001  Variation=dotnet  Destination=DotNetMiddleware  Success=True
AccountId=2001  Variation=dotnet  Destination=DotNetMiddleware  Success=True   ← same every time
AccountId=3001  Variation=python  Destination=PythonMiddleware  Success=True
AccountId=4001  Variation=python  Destination=PythonMiddleware  Success=True
```

- [ ] Same `AccountId` always produces the same `Variation`
- [ ] `Success=True` on all lines (no HTTP errors to middleware)
- [ ] The batch repeats every 30 seconds

**Red flag:** If the same `AccountId` alternates between `python` and `dotnet`, the context key is not being passed to LaunchDarkly correctly.

---

### Checkpoint 6 — Middleware Endpoints Responding

```bash
# Python middleware health
curl http://localhost:8000/health

# DotNet middleware health
curl http://localhost:8080/health
```

Both should return HTTP `200`.

- [ ] Python middleware health → `200 OK`
- [ ] DotNet middleware health → `200 OK`

Explore the API docs:

| Service | URL |
|---|---|
| RabbitMQ Management UI | http://localhost:15672 &nbsp;(guest / guest) |
| Python Middleware docs | http://localhost:8000/docs |
| .NET Middleware Swagger | http://localhost:8080/swagger |

---

### Checkpoint 7 — RabbitMQ Queue is Draining

Open [http://localhost:15672](http://localhost:15672) → login with `guest` / `guest` → **Queues** tab → click `middleware-processing`.

- [ ] **Messages Ready** = 0 (worker consumes as fast as the producer publishes)

If **Messages Ready** keeps growing, the `message-worker` is not consuming — check its logs with `docker compose logs message-worker`.

---

### Checkpoint 8 — Live Flag Change Test (Proves Streaming Works)

This confirms the SDK receives real-time updates from LaunchDarkly without a restart:

1. In the LD dashboard, change the Default rule to **100% → `dotnet`**
2. Save the flag
3. Watch `docker compose logs message-worker --follow` — within ~5 seconds **all accounts** should switch to `DotNetMiddleware`
4. Change back to `90% python / 10% dotnet` to restore the original split

- [ ] Routing changed live without restarting any container

---

## Expected Log Output (Normal Operation)

```
AccountId=1001  Variation=python  Destination=PythonMiddleware  ProcessingTimeMs=12
AccountId=1001  Variation=python  Destination=PythonMiddleware  ProcessingTimeMs=9
AccountId=1001  Variation=python  Destination=PythonMiddleware  ProcessingTimeMs=11
AccountId=2001  Variation=dotnet  Destination=DotNetMiddleware  ProcessingTimeMs=8
AccountId=2001  Variation=dotnet  Destination=DotNetMiddleware  ProcessingTimeMs=7
AccountId=3001  Variation=python  Destination=PythonMiddleware  ProcessingTimeMs=10
AccountId=4001  Variation=python  Destination=PythonMiddleware  ProcessingTimeMs=9
```

- `AccountId=1001` → always **Python**
- `AccountId=2001` → always **DotNet**
- The batch of 7 messages repeats every 30 seconds

---

## Project Structure

```
src/
├── Shared/LaunchDarklyPOC.Shared     # DTOs, IXmlParser, AppConstants
├── XmlProducer/                       # Publishes XML messages to RabbitMQ
├── MessageWorker/                     # Consumes → evaluates flag → routes
│   └── appsettings.json              ← Edit SdkKey here
├── DotNetMiddleware/                  # New .NET system  (POST /process)
└── PythonMiddleware/                  # Mock TIBCO       (POST /process)
samples/                               # Example XML payloads
```
