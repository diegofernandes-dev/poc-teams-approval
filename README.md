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
[Approve] [Reject]
        |
        v
Azure Function
        |
Azure DevOps REST API
        |
        v
Approval approved/rejected
```

Teams is only the approval user interface. Azure DevOps remains authoritative.

## Important pipeline rule

An HML-only execution must stop after HML. It must not create a pending PRD approval and therefore must not generate a Teams notification.

```text
DEV -> HML -> END
```

Only an explicit production promotion should create the PRD approval flow.

## Current status

The POC foundation is in progress.

Completed:

- Azure subscription validated in the POC tenant.
- Monthly cost budget configured.
- Resource group created in `East US 2`.
- Azure Function App creation started using Flex Consumption.
- Function runtime selected as `.NET 10 (Isolated)`.
- Function instance size selected as `512 MB` for the POC.
- Public inbound access enabled for the POC.
- VNet integration disabled for the POC.
- Application Insights enabled.
- Durable Task Scheduler disabled.
- Azure OpenAI integration disabled.
- **Application slice 1:** Bot Framework messaging gateway (`POST /api/messages`) implemented in code (not yet deployed).

Detailed execution notes are in [`docs/hands-on-progress.md`](docs/hands-on-progress.md).

## Application (slice 1 — Bot messaging gateway)

This slice implements the minimum Azure Functions application required to receive Microsoft Bot Framework activities. It proves Azure Bot can POST to the Function, activities are deserialized and logged, and the endpoint returns the HTTP response expected by Bot Framework integration.

### How this slice fits the larger POC

```text
[future] Azure DevOps Service Hook
        |
        v
Approval Gateway Function App   <-- slice 1 starts here (Bot /api/messages only)
        |
        v
[future] Teams personal message + Adaptive Card
        |
        v
[future] Azure DevOps REST API
```

Slice 1 covers only the Bot Framework inbound path. Azure DevOps hooks, Adaptive Cards, proactive messaging, and approval logic are intentionally deferred.

### Solution layout

```text
ApprovalGateway.slnx
src/ApprovalGateway/          Azure Functions isolated worker (.NET 10)
  Functions/BotMessagesFunction.cs   POST /api/messages
  Functions/HealthFunction.cs        GET /api/health
  Bot/ApprovalGatewayAgent.cs        minimal reply + structured logging
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
POST https://func-ado-teams-poc-diegolab.azurewebsites.net/api/messages
GET  https://func-ado-teams-poc-diegolab.azurewebsites.net/api/health
```

### Manual next step — configure Azure Bot messaging endpoint

After deploying the function package to `func-ado-teams-poc-diegolab`:

1. Set Function App application settings: `MicrosoftAppId`, `MicrosoftAppTenantId`, `MicrosoftAppPassword`.
2. In Azure Portal, open `bot-ado-teams-poc-diegolab` → **Settings** → **Configuration**.
3. Set **Messaging endpoint** to:

   ```text
   https://func-ado-teams-poc-diegolab.azurewebsites.net/api/messages
   ```

4. Use **Test in Web Chat** or Teams to send a message; expect reply: `Approval Gateway POC is online.`

### Auth note (managed identity tradeoff)

The Function App system-assigned managed identity is separate from the Bot App Registration identity. This slice uses **client secret** auth (`MicrosoftAppPassword`) for outbound Bot Framework calls. Secretless auth via user-assigned managed identity requires reconfiguring the Azure Bot resource and is deferred to a future infrastructure slice.

### Not implemented in slice 1

- Azure DevOps REST API or Service Hooks
- approval-pending / approval-completed handling
- Adaptive Cards, Approve/Reject actions
- proactive Teams messages
- conversation persistence (uses in-memory storage only)
- Cosmos DB, Table Storage, queues, Durable Functions
- Graph API, approver lookup, Key Vault, APIM
- Teams app manifest / installation
- CI/CD pipeline
- infrastructure changes (Bicep unchanged)

## Production security note

The POC temporarily allows public access to the Function App to keep the first implementation small. This is **not** the intended production posture for a production-deployment approval gateway.

A production design must minimize the public attack surface and place the approval gateway behind controlled ingress/private networking where possible, while accounting for Azure DevOps Service Hooks and Microsoft Teams/Bot SaaS connectivity.

A dedicated production-private-network guide will be added after the functional POC is working.

## Cost principle

The POC aims to stay at or near zero cost and avoids unnecessary infrastructure such as AKS, Cosmos DB, Redis, Service Bus, APIM, and Application Gateway until a concrete requirement justifies them.
