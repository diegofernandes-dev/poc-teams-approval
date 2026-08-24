# Teams app package (personal bot)

Minimum Microsoft Teams application package that sideloads the existing Azure Bot as a **personal** Teams app.

## What this package does

It declares a Teams app whose only capability is a personal-scoped bot. After you upload the ZIP in Teams and open the personal app, normal text chat is routed:

```text
Teams personal app
  -> Azure Bot (Teams channel)
  -> Azure Function POST /api/messages
  -> reply
```

The package contains **no app logic** and **no secrets**. Runtime Adaptive Card behavior lives in Azure (`func-ado-teams-poc-diegolab` + `bot-ado-teams-poc-diegolab`).

## Teams App ID vs Azure Bot App ID

| ID | Manifest field | Value | Role |
| --- | --- | --- | --- |
| **Teams App ID** | root `id` | `831041ff-1d21-4a08-958e-02b17c10d7c2` | Identifies this Teams app package in Teams catalogs / sideload updates |
| **Bot (Microsoft) App ID** | `bots[].botId` | `5936429a-7889-45c1-983e-d9064aa7ee84` | Existing SingleTenant Entra / Azure Bot client ID |

These GUIDs are intentionally **different**. Do not rotate or replace the Bot App ID when rebuilding the ZIP unless you also change the Azure Bot registration.

## Package layout

```text
teams/
  appPackage/
    manifest.json   # schema 1.30
    color.png       # 192x192
    outline.png     # 32x32 white on transparent
  privacy.md
  terms.md
  README.md
```

ZIP content (at archive root only): `manifest.json`, `color.png`, `outline.png`.

## Build the ZIP

From the repository root:

```bash
./scripts/teams/build-app-package.sh
```

Output (gitignored):

```text
build/teams/ApprovalGateway.zip
```

## Manual installation (POC)

Prerequisites:

- Tenant allows **custom app upload** / sideloading for your account.
- Azure Bot Teams channel is enabled and the messaging endpoint points at the Function `/api/messages` URL (already validated via Web Chat for this POC).

Steps:

1. Build the ZIP (command above).
2. In Microsoft Teams: **Apps** → **Manage your apps** → **Upload an app** → **Upload a custom app**.
3. Select `build/teams/ApprovalGateway.zip`.
4. Add the app for yourself (personal scope).
5. Open the personal app and send a normal text message.

### Expected test

1. Open the personal app chat.
2. Send: `hello`
3. Expect reply: `Approval Gateway POC is online.`
4. Send: `card`
5. Expect a fake Adaptive Card with Approve / Reject.
6. Click Approve → `POC action received: approve`
7. Click Reject → `POC action received: reject`
8. After deploy of the proactive slice: trigger `POST /api/poc/proactive` with a Function key (see root README) → expect `Proactive Teams notification POC.` in the same personal chat (requires a prior inbound message on the same warm instance).

Buttons do not call Azure DevOps. Card payloads are untrusted POC input only.

## Explicitly out of scope (future slices)

- Real Approve/Reject decisions or Azure DevOps REST approval APIs
- Azure DevOps Service Hooks
- Durable conversation persistence / approver routing (POC uses temporary in-memory routing state only)
- SSO, Graph permissions, tabs, message extensions
- Publishing to org catalog or Teams Store
- `Action.Submit` fallback for older Teams clients (POC uses `Action.Execute` only)

## Security notes

- The ZIP must never contain `MicrosoftAppPassword`, client secrets, or admin credentials.
- This manifest requests **no** `permissions`, **no** Graph scopes, and **no** `webApplicationInfo` (SSO).
- Bot scope is **personal only** (`team` / `groupChat` omitted).
- `validDomains` is omitted (bot-only; no tabs / SSO / OAuth pages).
