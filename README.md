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

Detailed execution notes are in [`docs/hands-on-progress.md`](docs/hands-on-progress.md).

## Production security note

The POC temporarily allows public access to the Function App to keep the first implementation small. This is **not** the intended production posture for a production-deployment approval gateway.

A production design must minimize the public attack surface and place the approval gateway behind controlled ingress/private networking where possible, while accounting for Azure DevOps Service Hooks and Microsoft Teams/Bot SaaS connectivity.

A dedicated production-private-network guide will be added after the functional POC is working.

## Cost principle

The POC aims to stay at or near zero cost and avoids unnecessary infrastructure such as AKS, Cosmos DB, Redis, Service Bus, APIM, and Application Gateway until a concrete requirement justifies them.
