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

Expected and validated RBAC:

### Host Storage

Function managed identity:

```text
func-ado-teams-poc-diegolab
```

Role:

```text
Storage Blob Data Owner
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

Scope:

```text
app-package-func-ado-teams-poc-diegolab-1f4bef4
```

The `Storage Blob Data Owner` assignment from the parent Storage Account is also inherited by the container.

### Application Insights

Role:

```text
Monitoring Metrics Publisher
```

The role assignment to the Function App system-assigned managed identity was explicitly validated after deployment.

## 12. Tags

Applied to the resources created by the Function wizard:

```text
project = poc-teams-approval
environment = poc
```

## 13. Provisioned resources

Deployment completed successfully.

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

This was not manually deleted. Any `DefaultResourceGroup-*` created by Azure should be inspected for automatically managed Azure Monitor/Application Insights resources before considering cleanup.

## 14. Architecture constraints to preserve

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

## 15. Planned Azure DevOps events

Primary event:

```text
ms.vss-pipelinechecks-events.approval-pending
```

Also required:

```text
ms.vss-pipelinechecks-events.approval-completed
```

The completed event will later be used to update/remove Adaptive Card actions when an approval is completed directly in Azure DevOps.

## 16. Current checkpoint

Azure infrastructure foundation for the Approval Gateway is provisioned and validated.

Validated:

- Function App deployment completed;
- Flex Consumption plan exists;
- Storage Account exists;
- deployment package container exists;
- Application Insights exists;
- system-assigned Managed Identity exists;
- Storage Blob Data Owner assignment exists;
- deployment container Storage Blob Data Contributor assignment exists;
- Application Insights Monitoring Metrics Publisher assignment exists.

## 17. Next checkpoint

Proceed to the next integration layer only after deciding the order of:

1. Azure Bot / Teams application setup;
2. gateway application skeleton and HTTP endpoints;
3. Azure DevOps test project/environment/approval configuration.

The walkthrough must continue one validated checkpoint at a time.
