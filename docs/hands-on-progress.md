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

The Function App will act as the Approval Gateway between Azure DevOps and Microsoft Teams.

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
- Version: `.NET 10 (Isolated)`
- Instance size: `512 MB`
- Zone redundancy: disabled

The 512 MB size is intentional for the initial low-volume webhook/callback workload. It can be increased if runtime pressure appears.

## 5. Function Storage

The Function creation wizard proposed a new Storage Account:

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

New resource proposed in:

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

## 10. Architecture constraints to preserve

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

## 11. Planned Azure DevOps events

Primary event:

```text
ms.vss-pipelinechecks-events.approval-pending
```

Also required:

```text
ms.vss-pipelinechecks-events.approval-completed
```

The completed event will later be used to update/remove Adaptive Card actions when an approval is completed directly in Azure DevOps.

## 12. Next checkpoint

Continue the Function App creation wizard from the step immediately after Durable Functions.

Do not assume the Function App exists until deployment has been explicitly reviewed, created, and validated.
