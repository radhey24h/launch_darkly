# LaunchDarkly Setup Guide

Complete step-by-step instructions for setting up the `backend-routing` feature flag used in this POC.

---

## Prerequisites

- A LaunchDarkly account ([free trial at app.launchdarkly.com](https://app.launchdarkly.com))
- Admin or write access to a LaunchDarkly project

---

## Step 1 — Create a Project

1. Log in to [app.launchdarkly.com](https://app.launchdarkly.com)
2. Click **Projects** in the left sidebar
3. Click **Create project**
4. Fill in:
   - **Name**: `CanaryPOC`
   - **Key**: `canary-poc` (auto-filled)
5. Click **Save project**

> **[Screenshot placeholder: New Project dialog]**

LaunchDarkly automatically creates two environments:
- `Production` (green)
- `Test` (yellow)

---

## Step 2 — Create a Development Environment

1. In your project, click **Environments** → **Create environment**
2. Fill in:
   - **Name**: `Development`
   - **Key**: `development`
   - **Color**: Blue (optional)
3. Click **Save environment**

> **[Screenshot placeholder: Environments list with Development added]**

---

## Step 3 — Get the SDK Key

1. Click **Environments** → click on `Development`
2. Find **SDK key** — it looks like `sdk-xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx`
3. Click the copy icon to copy it

> **[Screenshot placeholder: SDK key highlighted in the environment settings page]**

> ⚠️ **Important**: 
> - Use the **Server-side SDK key** (starts with `sdk-`)
> - Never use the **Client-side ID** (starts with `61`) — that's for browser/mobile SDKs
> - Never commit this key to source control

4. Paste the key into `src/Gateway/appsettings.json`:

```json
{
  "LaunchDarkly": {
    "SdkKey": "sdk-YOUR-ACTUAL-KEY-HERE"
  }
}
```

Or set as an environment variable (recommended for production):

```bash
# Windows PowerShell
$env:LaunchDarkly__SdkKey = "sdk-your-key-here"

# Windows CMD
set LaunchDarkly__SdkKey=sdk-your-key-here

# Linux / macOS
export LaunchDarkly__SdkKey=sdk-your-key-here
```

---

## Step 4 — Create the Feature Flag

### 4a. Navigate to Feature Flags

1. In the left sidebar, click **Feature flags**
2. Click **Create flag**

> **[Screenshot placeholder: Empty feature flags list with Create flag button]**

### 4b. Configure the Flag

Fill in the following:

| Field | Value |
|-------|-------|
| **Name** | `Backend Routing` |
| **Key** | `backend-routing` ← **must match exactly** |
| **Description** | Routes traffic between .NET and Python backends |
| **Flag type** | String |
| **Temporary flag** | No (this is a permanent migration flag) |

> **[Screenshot placeholder: Create flag form with fields filled in]**

### 4c. Configure Variations

Under **Variations**, set up two string variations:

| # | Name | Value | Description |
|---|------|-------|-------------|
| 1 | DotNet | `dotnet` | New .NET backend |
| 2 | Python | `python` | Legacy Python/TIBCO backend |

> ⚠️ The **Value** field must be exactly `dotnet` and `python` (lowercase).
> These match the string constants in `BackendRouter.cs`.

> **[Screenshot placeholder: Variations section with dotnet and python configured]**

### 4d. Set Default Variations

Under **Default variations**:

- **Serve when targeting is ON**: `python` (we want Python as the conservative default)
- **Serve when targeting is OFF**: `python` (fallback when flag is disabled)

Click **Save flag**.

> **[Screenshot placeholder: Default variations section]**

---

## Step 5 — Configure the Percentage Rollout

This is the core of the canary deployment pattern.

### 5a. Open the Flag in Development Environment

1. Click on `Backend Routing` flag
2. Select the **Development** environment tab
3. Toggle the flag **ON** (blue toggle in top right)

> **[Screenshot placeholder: Flag detail page with environment tabs and ON toggle]**

### 5b. Set Up Default Rule (Percentage Rollout)

1. Scroll to **Default rule**
2. Change the rule type from "Serve variation" to **"Percentage rollout"**
3. Configure the percentages:

| Variation | Percentage | Description |
|-----------|-----------|-------------|
| `dotnet`  | **10%** | New .NET backend (canary) |
| `python`  | **90%** | Legacy Python/TIBCO backend |

4. Verify the total adds up to **100%**

> **[Screenshot placeholder: Percentage rollout slider showing 10% dotnet / 90% python]**

5. Click **Save changes**
6. Click **Review and save** → **Save changes**

---

## Step 6 — Verify the Flag is Working

### Using the LaunchDarkly Debugger

1. In the flag detail page, click **Live events** (or **Debugger**)
2. Run a test request from the Gateway:

```bash
curl http://localhost:5000/api/orders/alice
```

3. In the LaunchDarkly debugger, you should see an evaluation event:
   - **Flag**: `backend-routing`
   - **Context key**: `alice`
   - **Variation**: `python` (or `dotnet` depending on the hash)

> **[Screenshot placeholder: Live events tab showing flag evaluation with context key and variation]**

---

## Step 7 — Demonstrate Sticky Routing

The key feature of LaunchDarkly percentage rollouts is **deterministic hashing**.

LaunchDarkly computes:
```
bucket = murmurhash3(flagKey + "." + contextKey) % 10000
```

If `bucket < 1000` (i.e., within the 10% range), the user gets `dotnet`, otherwise `python`.

Because the hash is deterministic, the same user ALWAYS gets the same backend.

### Test with sample users

Run these curl commands and observe the `selectedVariation` field:

```bash
# Try each user multiple times — the variation never changes
for user in alice bob charlie david emma; do
  echo -n "$user: "
  curl -s http://localhost:5000/api/orders/$user | python -m json.tool | grep selectedVariation
done
```

Expected output (variations depend on actual hash values):
```
alice:   "selectedVariation": "python"
bob:     "selectedVariation": "python"
charlie: "selectedVariation": "python"
david:   "selectedVariation": "python"
emma:    "selectedVariation": "python"
```

With a 10% rollout, approximately 1 in 10 users will get `dotnet`.
Users with hashes like `user001`, `user009`, etc. may land in the dotnet bucket.

---

## Step 8 — Progressive Rollout Stages

As you gain confidence, increase the .NET percentage in the LaunchDarkly dashboard:

| Stage | .NET | Python | When to proceed |
|-------|------|--------|-----------------|
| Initial | 10% | 90% | Start here |
| Phase 2 | 25% | 75% | After 24h with no errors |
| Phase 3 | 50% | 50% | After 48h stable |
| Phase 4 | 75% | 25% | After 1 week stable |
| Complete | 100% | 0% | Migration complete |

**No application restart or redeployment needed** — the SDK streams the change within seconds.

---

## Step 9 — Instant Rollback

If the .NET backend has issues:

1. Go to the LaunchDarkly dashboard
2. Find `backend-routing` flag
3. Change `dotnet` to **0%** and `python` to **100%**
4. Click **Save changes**

The Gateway picks up the change within ~30 seconds.

> **[Screenshot placeholder: Rollback showing 0% dotnet, 100% python]**

---

## Troubleshooting

### "LaunchDarkly SDK did not initialize"

- Verify `SdkKey` is set correctly in `appsettings.json` or environment variable
- Check network connectivity to `stream.launchdarkly.com`
- Firewall: LaunchDarkly requires outbound HTTPS (port 443) to `*.launchdarkly.com`
- The Gateway will use the `DefaultVariation` ("python") as fallback — it will still work

### "Flag evaluates but always returns 'python'"

- Verify the flag is **turned ON** in the correct environment
- Check you're using the SDK key for the correct environment
- With 10% rollout, most users get "python" — this is expected
- Use the LaunchDarkly Debugger to confirm evaluations are being received

### "404 from backend"

- Ensure both backend services are running before the Gateway starts
- Verify `appsettings.json` backend URLs match where the services are running
- Check `docker-compose.yml` service names match the URL configuration

---

## Environment-Specific SDK Keys

| Environment | SDK Key Format | Use Case |
|-------------|---------------|----------|
| Development | `sdk-dev-...` | Local development |
| Test / QA | `sdk-test-...` | CI/CD pipelines |
| Production | `sdk-prod-...` | Live traffic |

Always use the correct key for each environment. Mixing environments can cause unexpected flag evaluations.
