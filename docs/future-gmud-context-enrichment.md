# Future evolution — GMUD context enrichment

> Status: **future evolution / explicitly out of current POC scope**.
>
> Current priority remains proving the basic approval flow end-to-end before enriching the approval request.

## Goal

The Teams approval request should not arrive "blind" when the production approval flow is connected to Azure DevOps.

The intent is **not** to turn Teams into the GMUD system. Teams remains only the approval user interface.

The future card should contain enough context for the approver to understand what is being deployed and why, with links back to the authoritative systems when deeper inspection is required.

## Source-of-truth boundaries

The current organizational model uses **SharePoint to manage GMUD records**.

The intended future responsibility split is:

```text
SharePoint
  = source of truth for GMUD/change information

Azure DevOps
  = source of truth for deployment approval state,
    approvers, authorization, environment protection and audit

Teams
  = approval user interface

Approval Gateway
  = correlation and presentation layer
```

The gateway must not duplicate SharePoint as a GMUD database and must not become the approval authority.

## Correlation model

The preferred future contract is for the pipeline to carry only the minimum stable GMUD correlation identifier, for example:

```text
changeId = CHG-2026-004182
```

The pipeline should **not** copy all GMUD fields into pipeline variables.

Instead, when an Azure DevOps `approval-pending` event is received, the Approval Gateway can combine:

1. Azure DevOps deployment/run metadata;
2. the GMUD identifier associated with the deployment;
3. GMUD details read from SharePoint;
4. optional technical change information derived from the deployment artifact/build.

Conceptually:

```text
Azure DevOps approval-pending
          |
          v
   Approval Gateway
      /         \
     /           \
ADO/run data   SharePoint GMUD
     \           /
      \         /
       enriched context
             |
             v
       Adaptive Card
             |
             v
           Teams
```

## Candidate approval-card context

A future production approval card may show a concise subset such as:

- GMUD ID;
- GMUD title/summary;
- application;
- target environment;
- risk/classification;
- planned deployment window;
- responsible/requester;
- short rollback summary;
- deployment/build/run identifier;
- candidate artifact/image version or digest;
- short technical changelog;
- link to the full GMUD in SharePoint;
- link to the Azure DevOps run.

Example only:

```text
Production deployment approval

GMUD: CHG-2026-004182
Application: payments-api
Environment: PRD
Risk: Medium
Window: 23:00-23:30

Change summary
- Fix duplicate processing
- Add retry policy
- Dependency update

Rollback
Return to previous production artifact

[View GMUD] [View Pipeline]
[Approve] [Reject]
```

The card should remain concise. Large descriptions, complete change records, or long commit histories belong in SharePoint/Azure DevOps, not in Teams.

## Technical changelog enrichment

A later evolution may enrich the approval request with a technical delta derived from the artifact/build being promoted.

The preferred semantic comparison is:

```text
artifact currently deployed in PRD
            vs
candidate artifact being promoted
```

Potential sources include:

- Git commits;
- pull requests;
- Azure Boards work items;
- artifact/image digest;
- build/run metadata;
- dependency changes;
- database migration indicators;
- infrastructure/configuration changes.

This technical context is separate from GMUD business/change-management data.

## Optional deployment context contract

If needed, pipelines may later publish a small structured technical context artifact, for example:

```json
{
  "application": "payments-api",
  "environment": "prd",
  "buildId": 12345,
  "gitSha": "91ac83f",
  "image": "payments-api@sha256:...",
  "changeId": "CHG-2026-004182"
}
```

The `changeId` is the correlation key. GMUD fields such as risk, rollback, title and window should continue to come from SharePoint rather than being duplicated into this artifact.

## Why defer this

This enrichment is intentionally deferred until the basic POC proves:

```text
Azure DevOps approval pending
  -> gateway
  -> proactive Teams card
  -> authenticated Approve/Reject
  -> Azure DevOps decision
```

Only after that flow is stable should SharePoint/GMUD enrichment be added.

This keeps the current POC focused and avoids mixing approval transport, authorization, SharePoint integration and presentation enrichment before the core workflow is proven.
