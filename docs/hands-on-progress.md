# Hands-on Progress

This document records the POC configuration checkpoints as they are completed.

> Scope note: Microsoft 365 corporate account/tenant creation is intentionally omitted. The document starts from the Azure foundation.

## 1. Azure subscription

Validated an existing Azure subscription in the POC tenant.

Observed state:

- Subscription: `Azure subscription 1`
- Directory: `Diegolab`
- Status: `Active`
- Role: `Owner`
- Plan: `Azure Plan`
- Initial resource count: none

Decision: reuse the existing subscription instead of creating another one.

## 2. Cost guardrail

Created a monthly Azure Cost Management budget before provisioning application resources.

Budget:

- Name: `poc-monthly-budget`
- Reset period: monthly
- Amount: `50`
- Alerts:
  - 50%
  - 80%
  - 100%
- Action Group: none
- Email notification enabled

Purpose: provide early warning if the POC starts generating unexpected cost. The budget is an alerting control, not a hard spending cap.

## 3. Resource group

Created:

```text
rg-ado-teams-poc
```

Region:

```text
East US 2
```

Reason for `East US 2`: the POC has no Brazilian data-residency or latency requirement, so a broad US region was preferred over Brazil South to reduce expected cost and maximize service availability.

## 4. Azure Function App — creation configuration

The Function App acts as the Approval Gateway between Azure DevOps and Microsoft Teams.

### Hosting

Selected:

```text
Flex Consumption
```

Purpose: serverless execution with no always-on instance requirement for the POC.

### Basics

Configured:

- Resource group: `rg-ado-teams-poc`
- Function App name: `func-ado-teams-poc-diegolab`
- Region: `East US 2`
- Runtime stack: `.NET`
- Version: `.NET 10 (LTS), isolated worker model`
- Instance size: `512 MB`
- Zone redundancy: disabled
- Secure unique default hostname: enabled

The 512 MB size is intentional for the initial low-volume webhook/callback workload. It can be increased if runtime pressure appears.

## 5. Function Storage

The Function creation wizard created the Storage Account:

```text
rgadoteamspoc9a37
```

Decision: use the generated account for the POC.

Blob service diagnostic settings:

```text
Configure later
```

Reason: Storage diagnostics are not required to prove the approval flow, and enabling extra telemetry would add cost/noise.

Azure DevOps remains the approval-state authority. Any application storage introduced later is only technical/correlation state.

The Flex Consumption deployment package container was also created inside this account:

```text
app-package-func-ado-teams-poc-diegolab-1f4bef4
```

## 6. Azure OpenAI

Setting:

```text
Add Azure OpenAI resource and vector database: Disabled
```

Reason: no AI or vector capability is required by the approval gateway.

## 7. Networking

POC settings:

```text
Enable public access: On
Enable virtual network integration: Off
```

Reason: Azure DevOps Service Hooks and Microsoft Teams/Bot callbacks must be able to reach the gateway while the functional POC is being built. Introducing a VNet/private ingress before validating the workflow would add unnecessary complexity.

### Security warning

This is a POC-only posture.

The production implementation must not treat an Internet-reachable Function App as the final security boundary for production deployment approvals.

The future production design must evaluate a controlled public edge and private backend, for example:

```text
Azure DevOps / Teams
        |
        | HTTPS public ingress
        v
Controlled edge
(APIM / WAF-capable ingress / equivalent)
        |
        | private path
        v
Private Endpoint
        |
Azure Function
```

The detailed private-network implementation will be documented separately after the functional POC works.

## 8. Application Insights

Configured:

```text
Enable Application Insights: Yes
```

Created:

```text
func-ado-teams-poc-diegolab
```

Region:

```text
East US 2
```

The resource is workspace-based and uses:

```text
/subscriptions/e979b0ce-3200-4e2c-9741-bfb368aadf25/resourceGroups/DefaultResourceGroup-EUS2/providers/Microsoft.OperationalInsights/workspaces/DefaultWorkspace-e979b0ce-3200-4e2c-9741-bfb368aadf25-EUS2
```

Reason: observability is useful even in the POC because the gateway will need to diagnose:

- Azure DevOps webhook delivery;
- approval-event processing;
- Teams/Bot callbacks;
- Azure DevOps REST API failures;
- correlation/idempotency problems.

## 9. Durable Functions

Configured:

```text
Create a Durable Task Scheduler resource: Disabled
```

Reason: the initial workflow is event-driven and does not require durable orchestration, fan-out/fan-in, or long-running workflow state.

## 10. Deployment settings

Configured:

```text
Continuous deployment: Disabled
Basic authentication: Disabled
```

Decision: do not introduce GitHub Actions until the gateway code exists and the functional flow has been proven. Basic authentication is intentionally disabled; future automated deployment should prefer federated identity/OIDC rather than deployment credentials.

## 11. Resource authentication and Managed Identity

The Function App was configured to use identity-based access rather than secrets wherever the wizard supported it.

Selected authentication:

```text
Host storage: Managed identity
Deployment storage: Managed identity
Application Insights: Managed identity
Managed identity type: System-assigned
```

Managed Identity principal ID:

```text
674fdcac-734f-4d83-9d09-89aecbd8931e
```

Validated RBAC:

### Host Storage

Role:

```text
Storage Blob Data Owner
```

Role assignment name:

```text
ac5dabde-0f09-58c2-b7f0-e7b5186c9961
```

Scope:

```text
Storage account rgadoteamspoc9a37
```

### Deployment package container

Role:

```text
Storage Blob Data Contributor
```

Role assignment name:

```text
a83330cc-1a44-5356-bc7a-ec1ed00a8eae
```

Scope:

```text
app-package-func-ado-teams-poc-diegolab-1f4bef4
```

### Application Insights

Role:

```text
Monitoring Metrics Publisher
```

Role assignment name:

```text
74d3277c-6b9f-5dbd-a6b6-7d826a724d02
```

## 12. Tags

Applied to the resources created by the Function wizard:

```text
project = poc-teams-approval
environment = poc
```

## 13. Provisioned resources

Resources confirmed in `rg-ado-teams-poc`:

```text
ASP-rgadoteamspoc-9431                App Service plan
func-ado-teams-poc-diegolab           Function App
func-ado-teams-poc-diegolab           Application Insights
rgadoteamspoc9a37                      Storage account
```

All are in `East US 2`.

Azure also created a separate resource group named:

```text
DefaultResourceGroup-EUS2
```

This resource group contains the default Log Analytics workspace used by the workspace-based Application Insights resource and must not be deleted while that dependency remains.

## 14. Infrastructure as Code adoption

After manually validating the Azure resources, the baseline was captured in Bicep under:

```text
infra/
  main.bicep
  modules/platform.bicep
  poc.bicepparam
  README.md
```

The first `what-if` exposed differences between Portal-created resources and the initial Bicep model, including:

- workspace-based Application Insights using `LogAnalytics` ingestion;
- Portal-generated RBAC role assignment GUIDs;
- the hidden Application Insights link tag on the Function App;
- Flex Consumption runtime behavior.

The Bicep was updated to adopt the existing state rather than create duplicate role assignments or convert Application Insights away from its workspace-based configuration.

### Flex Consumption correction

An initial adoption deployment failed because the template declared:

```text
FUNCTIONS_WORKER_RUNTIME
```

inside `siteConfig.appSettings`. Flex Consumption rejects this setting because the runtime is configured via:

```text
properties.functionAppConfig.runtime
```

`FUNCTIONS_WORKER_RUNTIME` and `FUNCTIONS_EXTENSION_VERSION` were removed from the Bicep app settings.

### Successful adoption

The adoption deployment was then executed successfully:

```text
az deployment sub create \
  --name poc-teams-approval-iac-adopt \
  --location eastus2 \
  --template-file infra/main.bicep \
  --parameters infra/poc.bicepparam
```

Result:

```text
provisioningState: Succeeded
```

Correlation ID:

```text
1b61aa55-4284-46f0-85ba-3833fb2060db
```

The deployment output confirmed the same Function Managed Identity principal ID:

```text
674fdcac-734f-4d83-9d09-89aecbd8931e
```

and the existing Storage, Application Insights, Function App, plan, deployment container, and RBAC resources were included in the deployment output.

### Post-adoption validation

A second `what-if` was run after the successful adoption. It showed no resource creates or deletes. Remaining `Modify` predictions were limited to ARM expression/default-value noise, including:

- concrete managed identity principal ID versus ARM `reference(...)` expression;
- resolved deployment container URL versus its ARM expression;
- Storage and Web App provider defaults.

The Function App system-assigned Managed Identity was re-read and remained:

```text
674fdcac-734f-4d83-9d09-89aecbd8931e
```

The three required role assignments were revalidated after adoption and remained unchanged:

```text
Monitoring Metrics Publisher   74d3277c-6b9f-5dbd-a6b6-7d826a724d02
Storage Blob Data Contributor  a83330cc-1a44-5356-bc7a-ec1ed00a8eae
Storage Blob Data Owner        ac5dabde-0f09-58c2-b7f0-e7b5186c9961
```

The Azure infrastructure foundation is therefore considered closed and managed through Bicep for subsequent Azure-side changes.

## 14.1. Azure Bot IaC adoption slice

After manually validating the Azure Bot and Function App bot settings, a second IaC slice captures:

- Azure Bot `bot-ado-teams-poc-diegolab` (`Microsoft.BotService/botServices@2022-09-15`);
- Microsoft Teams channel (`MsTeamsChannel` child resource);
- Function App bot settings: `MicrosoftAppId`, `MicrosoftAppTenantId`, `MicrosoftAppPassword`.

Files added/updated:

```text
infra/modules/bot.bicep
infra/main.bicep
infra/modules/platform.bicep
infra/poc.bicepparam
infra/README.md
```

Design decisions:

- Entra App Registration remains external — referenced by `botMicrosoftAppId` / `botTenantId` parameters only;
- `microsoftAppPassword` is a `@secure()` deploy-time parameter — read from `MICROSOFT_APP_PASSWORD` environment variable via `readEnvironmentVariable`, never committed;
- `siteConfig.appSettings` uses replace semantics — the template declares the full authoritative settings list;
- `APPINSIGHTS_INSTRUMENTATIONKEY` is omitted — redundant with `APPLICATIONINSIGHTS_CONNECTION_STRING`;
- Web Chat and Direct Line channels remain Portal-managed — only `MsTeamsChannel` is declared in Bicep;
- Bot messaging endpoint is parameterized as `botMessagingEndpoint` with the validated hostname.

Validation workflow: `az bicep build`, then subscription `what-if` with secure password supplied at the CLI. No deployment until what-if is reviewed.

## 15. Architecture constraints to preserve

The implementation must preserve these rules throughout the POC:

1. Azure DevOps is the source of truth for approvers, authorization, approval state, audit, and environment protection.
2. Teams is only the approval user interface.
3. The gateway must not keep its own authoritative approver list.
4. A failed Teams notification must leave the Azure DevOps approval pending.
5. The system must never fail open or auto-approve because Teams/gateway processing failed.
6. Approve/Reject callbacks must re-read current Azure DevOps approval state and authorization before applying a decision.
7. Adaptive Card payload data alone must never be trusted for approval ID, user identity, or authorization.
8. HML-only pipeline executions must end at HML and must not create a PRD pending approval.
9. Only explicit production promotion should create the PRD approval and corresponding Teams notification.
10. Service Hooks should be filtered by environment where appropriate, rather than creating one subscription per pipeline.

## 16. Planned Azure DevOps events

Primary event:

```text
ms.vss-pipelinechecks-events.approval-pending
```

Also required:

```text
ms.vss-pipelinechecks-events.approval-completed
```

The completed event will later be used to update/remove Adaptive Card actions when an approval is completed directly in Azure DevOps.

## 17. Current checkpoint

Azure infrastructure foundation for the Approval Gateway is provisioned, validated, adopted by Bicep, and post-adoption checks are complete.

Application slice 1 (Bot messaging gateway) is implemented in code and validated locally with `dotnet build` / `dotnet test`. It is **not yet deployed** to `func-ado-teams-poc-diegolab`.

Validated:

- Function App deployment completed;
- Flex Consumption plan exists;
- Storage Account exists;
- deployment package container exists;
- Application Insights exists and is workspace-based;
- system-assigned Managed Identity exists and is unchanged after Bicep adoption;
- Storage Blob Data Owner assignment exists and is unchanged;
- deployment container Storage Blob Data Contributor assignment exists and is unchanged;
- Application Insights Monitoring Metrics Publisher assignment exists and is unchanged;
- Bicep adoption deployment completed successfully;
- post-adoption `what-if` shows no create/delete operations;
- `ApprovalGateway.slnx` builds and tests pass locally;
- `POST /api/messages` and `GET /api/health` implemented using Microsoft 365 Agents SDK on .NET 10 isolated worker.

## 18. Application slice 1 — Bot messaging gateway

Implemented the minimum Azure Functions application to receive Bot Framework activities.

Scope delivered:

- `src/ApprovalGateway/` — .NET 10 isolated worker, Flex Consumption compatible (no `FUNCTIONS_WORKER_RUNTIME` in deployable config);
- `POST /api/messages` — delegates to `IAgentHttpAdapter` / `ApprovalGatewayAgent`;
- `GET /api/health` — returns `{ "status": "ok" }`;
- structured logging of activity metadata (type, id, conversation, channel; no secrets or full payloads);
- inbound JWT validation via sample-owned `AspNetExtensions` (Public Cloud ABS semantics) + `BotAuthenticationMiddleware`;
- outbound Connector auth via `Microsoft.Agents.Authentication.Msal` (`AuthType: ClientSecret`);
- configuration bridge: `MicrosoftAppId`, `MicrosoftAppTenantId`, `MicrosoftAppPassword`;
- no explicit `MemoryStorage` registration (SDK per-turn fallback until state is needed);
- unit tests in `tests/ApprovalGateway.Tests/`.

Explicitly not implemented: Azure DevOps, Adaptive Cards, proactive messaging, persistence, CI/CD, Bicep changes.

## 19. Next checkpoint (historical)

Deploy the function package to `func-ado-teams-poc-diegolab`, configure Function App settings and the Azure Bot messaging endpoint, then validate round-trip connectivity via Web Chat or Teams.

After Bot connectivity is confirmed, proceed to proactive Teams messaging and Azure DevOps Service Hook integration in subsequent slices.

The walkthrough must continue one validated checkpoint at a time.

## 20. Teams personal app package

Created the minimum Microsoft Teams app package to install the existing Azure Bot as a personal Teams app.

Delivered:

- `teams/appPackage/manifest.json` — unified manifest schema **1.30**; personal bot only
- Teams App ID (new): `831041ff-1d21-4a08-958e-02b17c10d7c2`
- Bot App ID referenced: `5936429a-7889-45c1-983e-d9064aa7ee84`
- Bot scopes: `personal` only (no `team` / `groupChat`)
- No `permissions`, Graph, SSO (`webApplicationInfo`), tabs, or compose extensions
- `validDomains` omitted (bot-only; no tabs/SSO/OAuth)
- POC icons: `color.png` 192×192, `outline.png` 32×32
- Build script: `scripts/teams/build-app-package.sh` → `build/teams/ApprovalGateway.zip`
- Docs: `teams/README.md`, `teams/privacy.md`, `teams/terms.md`

Validated locally:

- manifest JSON syntax
- icons dimensions
- ZIP root layout (`manifest.json`, `color.png`, `outline.png`)
- JSON Schema Draft 4 validation against Microsoft Teams schema v1.30

Not done in this slice: sideload/upload to Teams, Adaptive Cards, proactive messaging, Azure DevOps Service Hooks, Azure resource changes, commit/push.

### Expected manual test (after sideload)

1. Upload `build/teams/ApprovalGateway.zip` as a custom app.
2. Open the personal app.
3. Send `hello`.
4. Expect: `Approval Gateway POC is online.`

## 21. Next checkpoint

Manually sideload the Teams package in a tenant that allows custom apps, confirm personal chat round-trip, then proceed to Adaptive Cards / proactive messaging and Azure DevOps integration in later slices.
