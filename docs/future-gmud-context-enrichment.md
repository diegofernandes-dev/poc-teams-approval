# Future evolution — change/GMUD context enrichment

> Status: **future evolution / explicitly out of the core approval POC scope**.
>
> Architecture authority: see `docs/adr/ADR-003-provider-agnostic-change-management.md`.
>
> The earlier assumption that SharePoint is the permanent GMUD source of truth is superseded. SharePoint is now only one possible provider adapter alongside Jira Service Management, ServiceNow, or another ITSM.

## Goal

The Teams approval request should not arrive blind. The approval UI should contain enough change context for an approver to understand what is being deployed and why, with links back to the authoritative systems when deeper inspection is required.

Teams does not become the change-management system and does not become the deployment approval authority.

## Responsibility boundaries

```text
Backstage
  = developer onramp for creating a change request

Change Management Contract
  = canonical provider-agnostic change model

Configured Change Provider
  = system of record for change/GMUD data
    (SharePoint, Jira Service Management, ServiceNow, ...)

Azure DevOps
  = deployment approval state, authorization, protected-resource policy and audit

Teams
  = approval interaction/presentation surface

Approval Gateway
  = correlation, orchestration and presentation layer
```

The gateway must not duplicate the external change-management system as a permanent GMUD database and must not become the approval authority.

## Correlation model

The preferred contract is for the production-promotion flow to carry only the minimum stable change identifier, for example:

```text
changeId = CHG-2026-004182
```

The pipeline should not copy the full GMUD/change record into variables.

When an Azure DevOps `approval-pending` event is received, the Approval Gateway combines:

1. Azure DevOps deployment/run metadata;
2. the canonical `changeId` associated with the deployment;
3. change details resolved through the configured provider;
4. optional technical change information derived from the deployment artifact/build.

Conceptually:

```text
Azure DevOps approval-pending
          |
          v
   Approval Gateway
      /         \
     /           \
ADO/run data   Change Provider
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

- change/GMUD ID;
- title/summary;
- application;
- target environment;
- risk/classification;
- planned deployment window;
- responsible/requester;
- short rollback summary;
- deployment/build/run identifier;
- candidate artifact/image version or digest;
- short technical changelog;
- link to the full change record;
- link to the Azure DevOps run.

Example only:

```text
Production deployment approval

Change: CHG-2026-004182
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

[View Change] [View Pipeline]
[Approve] [Reject]
```

The card should remain concise. Large descriptions, complete change records, or long commit histories belong in the change-management system/Azure DevOps, not in Teams.

## Technical changelog enrichment

A later evolution may enrich the approval request with a technical delta derived from the artifact/build being promoted.

The preferred semantic comparison is:

```text
artifact currently deployed in PRD
            vs
candidate artifact being promoted
```

Potential sources include Git commits, pull requests, Azure Boards work items, artifact/image digest, build/run metadata, dependency changes, database migration indicators, and infrastructure/configuration changes.

This technical context is separate from business/change-management data.

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

The `changeId` is the correlation key. Risk, rollback, title and deployment window should continue to come from the configured change provider rather than being duplicated into this artifact.

## MVP boundary

Change enrichment is added only after the approval identity path is safe enough for the selected MVP behavior. SharePoint integration is not required merely to demonstrate enrichment; it is implemented only if SharePoint is selected as the provider for the POC/rollout.
