# Architecture Decision Records

This directory contains the architecture decisions for the production-change approval platform.

**Canonical branch:** `main` in this repository (`diegofernandes-dev/poc-teams-approval`).

The branch `docs/architecture-decisions-mvp` is **superseded by `main`** as of GMUD F1.4. Do not treat it as architectural authority. It is retained as a historical baseline only — not deleted.

## Decision status

| ADR | Title | Status |
|---|---|---|
| ADR-001 | Azure DevOps remains deployment approval authority | Accepted |
| ADR-002 | Backstage is the change-request onramp | Accepted (F1.3 frontend) |
| ADR-003 | Change management is provider-agnostic | Accepted |
| ADR-004 | Teams approvals use delegated Azure DevOps user identity | Proposed / validation required |
| ADR-005 | CAB scheduling uses deferred approval plus sequential locking | Proposed / partially validated |

## Platform flow (target)

```text
Developer
  -> Backstage change request (/gmud)
  -> Change Management capability (canonical contract)
  -> provider adapter (optional: SharePoint / Jira / ServiceNow)
  -> changeId
  -> Azure DevOps pipeline
  -> HML
  -> PRD Pre-check Approval (manager)
  -> Teams approval UI (interaction channel)
  -> PRD Post-check Approval (CAB)
  -> deferred effective time / release slot
  -> Exclusive Lock (sequential)
  -> PRD deployment
```

SharePoint is **not a mandatory dependency**. Provider selection belongs behind the Change Management capability — not in the developer-facing GMUD creation experience.

Two technical decisions are intentionally not closed yet:

1. delegated user authentication from Teams to Azure DevOps with correct human audit identity (ADR-004);
2. supported programmatic control of Azure DevOps Deferred Approval effective time (ADR-005).

Until those are proven, the fallback is to keep Azure DevOps as the authoritative UI for the affected decision rather than inventing a second source of truth.
