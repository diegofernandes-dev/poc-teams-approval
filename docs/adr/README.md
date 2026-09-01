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
| ADR-006 | Change management backend contract | Accepted (F2.0 architecture) |
| ADR-007 | Change record authority and persistence ownership | Accepted (F2.0 architecture review) |
| ADR-008 | Multi-activity change execution plan | Accepted (F2.1.2 architecture) |

## Platform flow (target)

```text
Developer
  -> Backstage change request (/gmud)
  -> Change Management capability (canonical contract + platform index)
  -> provider adapter (optional: SharePoint / Jira / ServiceNow)
  -> changeId
  -> controlled change execution
```

SharePoint is **not a mandatory dependency**. Provider selection belongs behind the Change Management capability — not in the developer-facing GMUD creation experience.

### Optional future execution paths (not universal)

The following may apply when a change is executed through Azure DevOps protected resources — they are **not** implied by every GMUD:

```text
changeId correlation (optional)
  -> Azure DevOps pipeline / CD
  -> environment approvals (manager, CAB)
  -> Teams approval UI (interaction channel)
  -> deferred effective time / release slot
  -> Exclusive Lock (sequential)
  -> deployment to protected environment
```

See ADR-001, ADR-004, and ADR-005 for ADO/Teams-specific decisions.

Two technical decisions are intentionally not closed yet:

1. delegated user authentication from Teams to Azure DevOps with correct human audit identity (ADR-004);
2. supported programmatic control of Azure DevOps Deferred Approval effective time (ADR-005).

Until those are proven, the fallback is to keep Azure DevOps as the authoritative UI for the affected decision rather than inventing a second source of truth.
