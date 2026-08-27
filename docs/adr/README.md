# Architecture Decision Records

This directory contains the architecture decisions for the production-change approval MVP.

## Decision status

| ADR | Title | Status |
|---|---|---|
| ADR-001 | Azure DevOps remains deployment approval authority | Accepted |
| ADR-002 | Backstage is the change-request onramp | Accepted |
| ADR-003 | Change management is provider-agnostic | Accepted |
| ADR-004 | Teams approvals use delegated Azure DevOps user identity | Proposed / validation required |
| ADR-005 | CAB scheduling uses deferred approval plus sequential locking | Proposed / partially validated |

## MVP boundary

The MVP target flow is:

```text
Developer
  -> Backstage change request
  -> canonical changeId
  -> Azure DevOps pipeline
  -> HML
  -> PRD Pre-check Approval (manager)
  -> Teams approval UI
  -> PRD Post-check Approval (CAB)
  -> deferred effective time / release slot
  -> Exclusive Lock (sequential)
  -> PRD deployment
```

SharePoint is **not a mandatory MVP dependency**. It is an optional provider adapter used only if the POC needs to prove integration with an external change-management system. Jira Service Management, ServiceNow, or another ITSM must be replaceable without changing the Backstage or Azure DevOps approval contracts.

Two technical decisions are intentionally not closed yet:

1. delegated user authentication from Teams to Azure DevOps with correct human audit identity;
2. supported programmatic control of Azure DevOps Deferred Approval effective time.

Until those are proven, the fallback is to keep Azure DevOps as the authoritative UI for the affected decision rather than inventing a second source of truth.
