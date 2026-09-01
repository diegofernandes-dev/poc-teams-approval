# ADR-002 — Backstage is the change-request onramp

## Status

Accepted for F1.3 frontend slice. F2 backend integration requires a separate architecture review gate.

> **F3.0 proposed refinement:** [ADR-009](./ADR-009-change-authorization-model.md)
> makes the platform Change Management capability—not the Backstage UI and not
> Azure DevOps—the authority for business-change authorization evidence. Backstage
> remains the onramp and is the preferred future CAB workspace. The historical
> F1/F2 boundaries below describe the implementation baseline and are not rewritten.

## Context

Production change management (GMUD) in this organization spans multiple systems:

- **Backstage IDP** — developer portal where engineers discover catalog entities and request production changes.
- **Change Management capability** — platform-owned provider-neutral contract and backend (see [ADR-003](./ADR-003-provider-agnostic-change-management.md)).
- **Azure DevOps** — source of truth for deployment approval state, approvers, authorization, environment protection, and audit where ADO protected resources are involved (see [ADR-001](./ADR-001-azure-devops-approval-authority.md)).
- **Microsoft Teams** — approval interaction channel (Approval Gateway POC); not the change-record authority.

Developers need a single, catalog-aware entry point to **request a production change** without assuming deployment-only semantics (application version, artifact, pipeline). Firewall, database, DNS, infrastructure, and manual operational changes must fit the same onramp.

Provider-specific persistence (SharePoint, Jira Service Management, ServiceNow, or other ITSM) is a backend/provider decision behind `IChangeManagementProvider`. SharePoint is one optional adapter — not assumed by the frontend or canonical domain. See ADR-003 for provider architecture; this ADR does not duplicate it.

## Decision

1. **Backstage is the developer onramp for production change requests** — the GMUD creation experience lives in the IDP as a first-class route (`/gmud`), not in Scaffolder and not as a Teams form.
2. **Dedicated frontend plugin** (`@internal/plugin-change-management`) isolates GMUD from Scaffolder and enables a clean API boundary (`ChangeManagementApi`) for backend swap in F2.
3. **Generic change-management domain language** — the universal model describes *what* changes, *when*, *risk/reversal*, and *evidence*; not deployment-specific fields (environment PRD, artifact version) unless introduced later via conditional context or ADR.
4. **Catalog-backed target selection (F1.3)** — `targetRef` resolves to Backstage Catalog **Component** entities; future System/Resource targets require explicit ADR.
5. **Mock API in F1.3** — `MockChangeManagementApi` returns a stable mock ID; no ITSM/provider wiring until F2.
6. **Right rail is informational** — post-creation flow copy is generic; no Teams, CAB, ADO, or deployment implementation leakage in F1.3.

## Boundaries (non-negotiable)

| System | Responsibility |
|---|---|
| Backstage | Change-request onramp UX, catalog context, identity-derived requester, mock/real API boundary |
| Change Management capability | Canonical provider-neutral contract; provider adapter routing (see ADR-003) |
| Azure DevOps | Deployment approval authority where ADO protected resources apply — not replaced by Backstage GMUD screen |
| Teams | Approval interaction channel — not the GMUD creation system or change-record authority |

Backstage does **not** become the approval authority. It does **not** become the GMUD record database. Provider implementations remain behind the provider abstraction defined in ADR-003.

## F1.3 scope (implemented)

- Route `/gmud` via new Backstage frontend system
- `CreateChangeRequest` canonical frontend model (see [`gmud-create-screen.md`](../ui/gmud-create-screen.md))
- Four numbered form sections + informational right rail
- Target context references from catalog shown separately from change evidence; neutral zero-state for evidence when none exist
- Automated tests for plugin wiring, model shape, validation, navigation

**Out of scope:** backend persistence, ITSM providers, workflow engine, attachment storage, conditional GMUD types, expanded catalog target kinds.

## F2 gate

### F2.0 — complete (architecture scaffold)

Delivered in ADO commit `b2bed17`. See [ADR-006](./ADR-006-change-management-backend-contract.md):

- `ChangeManagementService` + `IChangeManagementProvider` + HTTP routes
- RBAC permissions for create/read
- In-memory fake provider (non-production)
- Frontend **still uses mock** — wiring deferred to F2.1

### F2.1+ — not started (requires architecture review)

After F2.0 sign-off:

- Wire `ChangeManagementApi` to backend
- Durable development provider + real `changeId` sequence
- Persistent idempotency
- **STOP** before SharePoint/Jira/ServiceNow adapters

## Related documents

- Provider architecture: [ADR-003](./ADR-003-provider-agnostic-change-management.md)
- Deployment approval authority: [ADR-001](./ADR-001-azure-devops-approval-authority.md)
- Normative UI contract: [`gmud-create-screen.md`](../ui/gmud-create-screen.md)
- Implementation handoff: [`implementation-progress.md`](../backstage/implementation-progress.md)
- Future GMUD enrichment: [`future-gmud-context-enrichment.md`](../future-gmud-context-enrichment.md)
- ADO implementation: `platform-devops-developer-portal` (Azure DevOps) — authoritative for **what is implemented**

## Consequences

- Positive: Single developer entry point aligned with catalog and identity; clean swap point for backend in F2.
- Positive: Domain language decoupled from deployment assumptions and provider-specific persistence.
- Trade-off: GMUD record persistence and approval orchestration remain deferred; bridge docs must report deviations when ADO code diverges from this ADR.
