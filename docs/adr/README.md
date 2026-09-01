# Architecture Decision Records

This directory contains the architecture decisions for the production-change approval platform.

**Canonical branch:** `main` in this repository (`diegofernandes-dev/poc-teams-approval`).

The branch `docs/architecture-decisions-mvp` is **superseded by `main`** as of GMUD F1.4. Do not treat it as architectural authority. It is retained as a historical baseline only — not deleted.

## Decision status

| ADR | Title | Status |
|---|---|---|
| ADR-001 | Azure DevOps remains deployment approval authority | Accepted historical baseline; proposed supersession by ADR-009 |
| ADR-002 | Backstage is the change-request onramp | Accepted (F1.3 frontend) |
| ADR-003 | Change management is provider-agnostic | Accepted |
| ADR-004 | Teams approvals use delegated Azure DevOps user identity | Proposed historical design; proposed supersession by ADR-009 |
| ADR-005 | CAB scheduling uses deferred approval plus sequential locking | Proposed historical design; proposed supersession by ADR-009 |
| ADR-006 | Change management backend contract | Accepted (F2.0 architecture) |
| ADR-007 | Change record authority and persistence ownership | Accepted (F2.0 architecture review) |
| ADR-008 | Multi-activity change execution plan | Accepted (F2.1.2 architecture) |
| ADR-009 | Change authorization model | Proposed (F3.0 architecture review) |

## Platform flow (target)

```text
Developer
  -> Backstage change request (/gmud)
  -> Change Management capability (canonical contract + platform index)
  -> provider adapter (optional: SharePoint / Jira / ServiceNow)
  -> changeId
  -> versioned authorization policy
  -> effective requirements + human/governance decisions
  -> authorized business change
  -> provider-neutral execution eligibility
  -> controlled change execution
```

SharePoint is **not a mandatory dependency**. Provider selection belongs behind the Change Management capability — not in the developer-facing GMUD creation experience.

### Proposed F3 authorization direction

Per [ADR-009](./ADR-009-change-authorization-model.md), humans authorize the
business change and execution systems consume a platform eligibility result. Azure
DevOps is one optional future execution adapter, not the canonical approval state:

```text
pipeline presents changeId + targetRef
  -> Change Management eligibility check
  -> ALLOW or DENY with reason
  -> execution-system technical controls
```

Teams remains a future individual-decision interaction channel. The preferred CAB
decision interface is a future Backstage CAB Workbench. Neither is a system of
record; the platform authorization ledger is authoritative.

ADR-001, ADR-004, and ADR-005 remain historical records of the ADO-centric POC.
ADR-009, if accepted, supersedes their approval-authority, Teams-to-ADO decision,
and CAB-as-ADO-check directions. Technical execution safety controls may still be
used without becoming business authorization authority.
