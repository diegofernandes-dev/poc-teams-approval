# ADR-002 — Backstage is the change-request onramp

## Status

Accepted for F1.3 frontend slice. F2 backend integration requires a separate architecture review gate.

## Context

Production change management (GMUD) in this organization spans multiple systems:

- **SharePoint** — intended record of truth for GMUD/change information (see [`future-gmud-context-enrichment.md`](../future-gmud-context-enrichment.md)).
- **Azure DevOps** — source of truth for deployment approval state, approvers, authorization, environment protection, and audit.
- **Microsoft Teams** — approval user interface (Approval Gateway POC).
- **Backstage IDP** — developer portal where engineers discover catalog entities, run software templates, and navigate operational context.

Developers need a single, catalog-aware entry point to **request a production change** without assuming deployment-only semantics (application version, artifact, pipeline). Firewall, database, DNS, infrastructure, and manual operational changes must fit the same onramp.

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
| SharePoint / ITSM (future) | GMUD record of truth — not implemented in F1.3 |
| Azure DevOps | Deployment approval authority — not replaced by Backstage GMUD screen |
| Teams | Approval UI — not the GMUD creation system |

Backstage does **not** become the approval authority. It does **not** duplicate SharePoint as a GMUD database.

## F1.3 scope (implemented)

- Route `/gmud` via new Backstage frontend system
- `CreateChangeRequest` canonical frontend model (see [`gmud-create-screen.md`](../ui/gmud-create-screen.md))
- Four numbered form sections + informational right rail
- Catalog evidence as contextual metadata; neutral zero-state when absent
- Automated tests for plugin wiring, model shape, validation, navigation

**Out of scope:** backend persistence, ITSM providers, workflow engine, attachment storage, conditional GMUD types, expanded catalog target kinds.

## F2 gate (not started)

F2 requires explicit architecture sign-off on the F1.3 domain model before:

- `ChangeManagementService` backend behind `ChangeManagementApi`
- Provider configuration (app-config + secrets)
- RBAC permissions for create/read
- ADR for ITSM integration approach

## Related documents

- Normative UI contract: [`gmud-create-screen.md`](../ui/gmud-create-screen.md)
- Implementation handoff: [`implementation-progress.md`](../backstage/implementation-progress.md)
- Future GMUD enrichment: [`docs/future-gmud-context-enrichment.md`](../future-gmud-context-enrichment.md)
- ADO implementation: `platform-devops-developer-portal` (Azure DevOps) — authoritative for **what is implemented**

## Consequences

- Positive: Single developer entry point aligned with catalog and identity; clean swap point for backend in F2.
- Positive: Domain language decoupled from deployment assumptions.
- Trade-off: GMUD record persistence and approval orchestration remain deferred; bridge docs must report deviations when ADO code diverges from this ADR.
