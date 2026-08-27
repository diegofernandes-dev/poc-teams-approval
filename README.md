# Azure DevOps Approvals via Microsoft Teams — POC

Hands-on proof of concept for approving Azure DevOps production deployments directly from Microsoft Teams while keeping Azure DevOps as the source of truth for approvers, approval state, authorization, audit, and environment protection.

## Target flow

```text
Azure DevOps Pipeline
        |
        v
Environment PRD
        |
Approvals & Checks
        |
approval-pending
        |
Azure DevOps Service Hook
        |
        v
Azure Function / Approval Gateway
        |
        v
Microsoft Teams personal message
        |
Adaptive Card
[Review in Azure DevOps]   ← current (correct identity)
[Approve] [Reject]         ← future: only with user-delegated ADO token (SSO/OBO)
        |
        v
Approval applied in Azure DevOps as the human approver
```

Teams is only the approval user interface. Azure DevOps remains authoritative.

## Important pipeline rule

An HML-only execution must stop after HML. It must not create a pending PRD approval and therefore must not generate a Teams notification.

```text
DEV -> HML -> END
```

Only an explicit production promotion should create the PRD approval flow.

## Current status

The POC foundation and the ADO → Teams notification path are largely proven. **In-Teams Approve/Reject with correct ADO audit identity is blocked pending architect decision** — see [`docs/architect-decision-teams-approval-identity.md`](docs/architect-decision-teams-approval-identity.md).

Completed:

- Azure subscription, budget, resource group, Function App (Flex Consumption, `.NET 10` isolated), Application Insights.
- **Bot messaging gateway** (`POST /api/messages`) validated via Azure Bot + Teams personal chat.
- **Adaptive Card actions** (fake POC card) and **proactive personal messaging**.
- **Conversation reference persistence** in Blob Storage (Flex-safe).
- **ADO Service Hook** `approval-pending` → Function → Teams Adaptive Card for `prd-teams-poc`.
- **Pipeline** with compile-time PRD gate (`promoteToPrd`); HML-only vs PRD promotion validated.
- **Identity finding:** service PAT PATCH attributes the approval to the PAT owner (wrong user in audit). Gateway **no longer applies** approvals with a service account; card deep-links to Azure DevOps until SSO/OBO (or equivalent) is approved.

Detailed execution notes: [`docs/hands-on-progress.md`](docs/hands-on-progress.md).  
Architect decision brief: [`docs/architect-decision-teams-approval-identity.md`](docs/architect-decision-teams-approval-identity.md).

## Application (slice 1 — Bot messaging gateway)

This slice implements the minimum Azure Functions application required to receive Microsoft Bot Framework activities. It proves Azure Bot can POST to the Function, activities are deserialized and logged, and the endpoint returns the HTTP response expected by Bot Framework integration.

### How this slice fits the larger POC

```text
[future] Azure DevOps Service Hook
        |
        v
Approval Gateway Function App   <-- Bot /api/messages + Adaptive Card actions
        |
        v
[current local] Teams personal Adaptive Card (fake POC Approve/Reject)
        |
        v
[next] proactive personal Teams messaging trigger (implemented locally; not deployed yet)
        |
        v
[future] Azure DevOps REST API
```

Slice 1 covered the Bot Framework inbound path. The Adaptive Card slice adds interactive `Action.Execute` buttons in personal chat only (fake POC data; no approval decisions). The proactive slice adds a temporary Function-key trigger that sends a plain-text proactive personal message using in-memory routing state. Azure DevOps hooks and real approval logic remain deferred.

### Solution layout

```text
ApprovalGateway.slnx
src/ApprovalGateway/          Azure Functions isolated worker (.NET 10)
  Functions/BotMessagesFunction.cs   POST /api/messages
  Functions/PocProactiveFunction.cs  POST /api/poc/proactive (POC only; Function key)
  Functions/HealthFunction.cs        GET /api/health
  Bot/ApprovalGatewayAgent.cs        message + Adaptive Card Action.Execute handlers
  Bot/PocApprovalCard.cs             fake POC Adaptive Card (schema 1.5)
  Proactive/                         temporary in-memory conversation reference + proactive send
tests/ApprovalGateway.Tests/
```

### .NET 10 runtime vs .NET 8 in build output

The application **targets and runs on .NET 10** (`net10.0` in `ApprovalGateway.csproj`, matching the Flex Consumption Function App runtime in Bicep). This POC intentionally uses **.NET 10 LTS**, not a pre-release .NET 11.

During `dotnet build`, you may still see paths containing `net8.0`. That is expected and does **not** mean the app was downgraded:

| Artifact | TFM | Why |
|----------|-----|-----|
| `ApprovalGateway.dll` (your worker) | **net10.0** | Application code and isolated worker process |
| `WorkerExtensions` (auto-generated under `obj/`) | **net8.0** | Required by `Microsoft.Azure.Functions.Worker.Sdk` — bridge between the Functions **host** and your isolated worker. The SDK enforces `net8.0` and fails the build if changed. |
| Some NuGet dependencies (e.g. M365 Agents SDK) | **net8.0** (package layout) | Libraries published as `lib/net8.0/`; the .NET 10 runtime loads them via binary compatibility |

```text
dotnet build
  WorkerExtensions -> .../net8.0/Microsoft.Azure.Functions.Worker.Extensions.dll   (host bridge)
  ApprovalGateway  -> .../net10.0/ApprovalGateway.dll                               (your app)
```

Only the worker assembly (`ApprovalGateway.dll`) executes your functions. The `net8.0` artifacts are platform/library internals, not the application target framework.

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local) (for local run only)
- [Azurite](https://learn.microsoft.com/azure/storage/common/use-azurite-storage-emulator) or Azure Storage Emulator (for local `AzureWebJobsStorage`)

### Build

From the repository root:

```bash
dotnet build ApprovalGateway.slnx
```

### Test

```bash
dotnet test ApprovalGateway.slnx
```

### Run locally

1. Copy the local settings template:

   ```bash
   cp src/ApprovalGateway/local.settings.json.example src/ApprovalGateway/local.settings.json
   ```

2. Set the required values in `src/ApprovalGateway/local.settings.json` (see below). Do not commit secrets.

3. Start the function host from the project directory:

   ```bash
   cd src/ApprovalGateway
   func start
   ```

4. Local endpoints:

   - `GET  http://localhost:7071/api/health`
   - `POST http://localhost:7071/api/messages` (Bot Framework; requires valid JWT from Azure Bot Service)
   - `POST http://localhost:7071/api/poc/proactive?code=<function-key>` (POC proactive trigger; requires Function key from `func start` host output)

For Teams/Web Chat testing against a local host, use a dev tunnel and a Bot client-secret auth configuration. Federated credentials and user-assigned managed identity do not work through a tunnel to a local agent.

### Required configuration

Set these on the Function App (Azure Portal → Configuration → Application settings) after deployment, or in `local.settings.json` for local development:

| Setting | Description |
|---------|-------------|
| `MicrosoftAppId` | Azure Bot App ID (`5936429a-7889-45c1-983e-d9064aa7ee84` for this POC) |
| `MicrosoftAppTenantId` | Entra tenant ID (`e9dbba09-e7a3-42be-9a2c-f82470024e00` for this POC) |
| `MicrosoftAppPassword` | Client secret from the Bot App Registration (Entra → Certificates & secrets). **Never commit this value.** |

For local development only, `local.settings.json` may also include:

| Setting | Description |
|---------|-------------|
| `AzureWebJobsStorage` | Storage connection (e.g. `UseDevelopmentStorage=true` with Azurite) |
| `FUNCTIONS_WORKER_RUNTIME` | `dotnet-isolated` — **local host only**; do not add to Flex Consumption deployed app settings |

Application Insights is already configured at infrastructure level via Bicep (`APPLICATIONINSIGHTS_CONNECTION_STRING`).

### Deployed endpoint (after deployment)

```text
POST https://func-ado-teams-poc-diegolab-b5crbkdncmcqb6a6.eastus2-01.azurewebsites.net/api/messages
POST https://func-ado-teams-poc-diegolab-b5crbkdncmcqb6a6.eastus2-01.azurewebsites.net/api/poc/proactive?code=<function-key>
GET  https://func-ado-teams-poc-diegolab-b5crbkdncmcqb6a6.eastus2-01.azurewebsites.net/api/health
```

After deployment, retrieve the Function key from Azure Portal → Function App → **Functions** → `PocProactive` → **Function keys** (or `default`). Do not commit keys.

(Flex Consumption default hostname; Azure Bot messaging endpoint already points here.)

### Manual next step — configure Azure Bot messaging endpoint

After deploying the function package to `func-ado-teams-poc-diegolab`:

1. Set Function App application settings: `MicrosoftAppId`, `MicrosoftAppTenantId`, `MicrosoftAppPassword`.
2. In Azure Portal, open `bot-ado-teams-poc-diegolab` → **Settings** → **Configuration**.
3. Set **Messaging endpoint** to:

   ```text
   https://func-ado-teams-poc-diegolab.azurewebsites.net/api/messages
   ```

4. Use **Test in Web Chat** or Teams to send a message; expect reply: `Approval Gateway POC is online.`

### Authentication model (slice 1)

**Inbound (`POST /api/messages`):**

- Trigger uses `AuthorizationLevel.Anonymous` (Bot Framework does not send Function Keys).
- JWT validation is performed by `BotAuthenticationMiddleware` using ASP.NET Core JwtBearer configured in `Configuration/AspNetExtensions.cs`.
- That helper is adapted from the [official Agents SDK quickstart](https://github.com/microsoft/Agents/blob/main/samples/dotnet/quickstart/AspNetExtensions.cs). `Microsoft.Agents.Hosting.AspNetCore` 1.6.150 does **not** ship an equivalent registration API; the package README mentions `AddAgentAspNetAuthentication`, but no assembly in 1.6.x/1.7.x exports it.
- The helper is trimmed to Azure Public Cloud + this SingleTenant bot. Public-cloud ABS issuers, Microsoft Bot Service Entra tenant issuers, tenant-specific issuers, ABS vs Entra OpenID metadata switching, audience/lifetime/signing-key validation, and `RequireSignedTokens` are preserved. Gov/China branches, `AzureBotServiceOnly`, and `AllowedCallers` are omitted because they are unused by this deployment.

**Outbound (Bot Framework Connector replies):**

- Uses the Agents SDK MSAL provider (`Microsoft.Agents.Authentication.Msal`) with `Connections:ServiceConnection` / `AuthType: ClientSecret`.
- Operator-facing settings remain `MicrosoftAppId`, `MicrosoftAppTenantId`, and `MicrosoftAppPassword`. `BotConfiguration` maps those into the SDK `Connections` and `TokenValidation` keys.
- **`MicrosoftAppPassword` is required** for this POC. It is the smallest supported option that works with the current SingleTenant Azure Bot App Registration without changing Azure Bot identity mode.
- The Function App **system-assigned managed identity is not the Bot identity**. They are separate principals. `AuthType: SystemManagedIdentity` / `UserManagedIdentity` would only work if the Azure Bot were reconfigured to use that managed identity as its Microsoft App ID — that Azure redesign is intentionally deferred.
- Preferred future secretless path (not implemented): keep the same SingleTenant bot app ID and use `AuthType: FederatedCredentials` with a user-assigned managed identity plus an Entra federated credential. That still requires Azure/Entra changes outside this slice.

**Storage:**

- This slice does **not** register SDK `IStorage` / `MemoryStorage` for agent turn state. `AgentApplicationOptions` falls back to a per-turn `MemoryStorage` when no `IStorage` is in DI.
- Proactive routing uses a separate **temporary in-memory POC store** (`InMemoryPocConversationReferenceStore`) that holds only the last Teams personal `ConversationReference` (technical routing fields). It is **not** durable and is **not** an approver directory. Production will need persisted technical conversation state (Cosmos/Table/Blob or equivalent).

### Proactive personal messaging slice (POC)

Proves the gateway can send a proactive personal Teams message to the already-installed POC user without requiring an inbound message at trigger time.

**Mechanism:** capture `ConversationReference` from inbound personal Teams activities via `Activity.GetConversationReference()`, store in temporary in-memory POC state, then send later with `CloudAdapter.ContinueConversationAsync(ClaimsIdentity, ConversationReference, ...)`.

**Microsoft guidance used:**

- [Proactive messaging in the Agents SDK](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/proactive-overview)
- [IChannelAdapter.ContinueConversationAsync](https://learn.microsoft.com/en-us/dotnet/api/microsoft.agents.builder.ichanneladapter.continueconversationasync)
- [Send proactive messages in Teams](https://learn.microsoft.com/en-us/microsoftteams/platform/bots/how-to/conversations/send-proactive-messages)

**Graph:** not required when the Teams app is already installed and a conversation reference was captured from an inbound activity.

**Trigger endpoint:** `POST /api/poc/proactive`

- `AuthorizationLevel.Function` — requires Function key (`?code=` or `x-functions-key` header).
- Marked as temporary POC functionality; not part of the production approval surface.
- Returns **404** when no personal conversation reference has been captured yet.

**Proactive message text:** `Proactive Teams notification POC.`

**Prerequisites for manual test:**

1. Teams personal app already installed for the test user.
2. Send at least one message in personal chat (captures routing state on the warm instance).
3. Call `POST /api/poc/proactive` with a valid Function key on the **same warm instance** that handled the chat.

**POC limitations:**

- In-memory only — restart, cold start, or scale-out to another instance loses the reference.
- Single last personal reference only (no approver directory).
- Does not store message bodies, tokens, JWTs, or arbitrary Teams payloads.
- Azure DevOps remains out of scope.

**Manual test (after deploy):**

```bash
# 1. Chat once in Teams personal app (hello)
# 2. Trigger proactive send (replace host + key)
curl -X POST "https://func-ado-teams-poc-diegolab-b5crbkdncmcqb6a6.eastus2-01.azurewebsites.net/api/poc/proactive?code=<function-key>"
```

Expect the plain-text proactive message in the same personal chat.

### Not implemented yet (future slices)

- Azure DevOps REST API or Service Hooks
- approval-pending / approval-completed handling
- real Approve/Reject decisions
- durable conversation persistence / approver routing
- Cosmos DB, Table Storage, queues, Durable Functions
- Graph API, approver lookup, Key Vault, APIM
- CI/CD pipeline
- infrastructure changes (Bicep unchanged for this slice)

### Teams personal app package

Minimum Teams app package for sideloading the existing Azure Bot as a **personal** app only. See [`teams/README.md`](teams/README.md).

- Build: `./scripts/teams/build-app-package.sh` → `build/teams/ApprovalGateway.zip`
- Teams App ID (manifest `id`) is distinct from the Azure Bot Microsoft App ID (`bots[].botId`)
- Manual install only (no automatic publish/install in this slice)
- Expected chat tests:
  - send `hello` → reply `Approval Gateway POC is online.`
  - send `card` → fake Adaptive Card with Approve/Reject (`Action.Execute`)
  - click Approve → `POC action received: approve`
  - click Reject → `POC action received: reject`
- Buttons do **not** call Azure DevOps. Card `data`/`verb` are untrusted client input used only for POC acknowledgement.

### Adaptive Card slice (deployed)

- Fake POC card only (Application `poc-api`, Environment `PRD`, Run `#12345`).
- Schema version **1.5**; buttons are **`Action.Execute`** with `verb`/`data.action` = `approve` | `reject`.
- Callback path: invoke `adaptiveCard/action` → `AdaptiveCards.OnActionExecute` → `AdaptiveCardInvokeResponseFactory.Message(...)`.
- **POC compatibility decision:** no `Action.Submit` fallback. This validates the modern invoke path against the current Teams client. Microsoft documents Submit fallback for maximum compatibility with older Teams clients; that is a production concern, not this slice.
- Security: future real approvals must obtain authenticated Teams/Entra identity, current ADO approval state, authorization/approver membership, and environment/run correlation from trusted server-side sources — never from the card payload alone.
- Function package published to `func-ado-teams-poc-diegolab` (Flex hostname below). Manual Teams test: send `card`, then Approve/Reject.

## Production security note

The POC temporarily allows public access to the Function App to keep the first implementation small. This is **not** the intended production posture for a production-deployment approval gateway.

A production design must minimize the public attack surface and place the approval gateway behind controlled ingress/private networking where possible, while accounting for Azure DevOps Service Hooks and Microsoft Teams/Bot SaaS connectivity.

A dedicated production-private-network guide will be added after the functional POC is working.

## Cost principle

The POC aims to stay at or near zero cost and avoids unnecessary infrastructure such as AKS, Cosmos DB, Redis, Service Bus, APIM, and Application Gateway until a concrete requirement justifies them.
