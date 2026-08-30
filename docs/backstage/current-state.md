# Backstage IDP — current state snapshot

> **Bridge repository:** `diegofernandes-dev/poc-teams-approval`  
> **Canonical architectural branch:** `main` (branch `docs/architecture-decisions-mvp` superseded as of F1.4)  
> **Implementation repository (ADO):** `platform-devops-developer-portal`  
> **Active branch:** `feat/ado-repo-governance`  
> **Last updated:** 2026-08-30 (F1.4 handoff)

## Stack

| Item | Value |
|---|---|
| Platform | Backstage 1.51.0 |
| Auth | Microsoft Entra ID + MS Graph catalog ingestion |
| RBAC | Community RBAC + ownership on Systems |
| Azure DevOps | Project access, repo governance, pipeline integration |
| TechDocs | AWS S3 (production path) |

## GMUD change management (F1.4)

| Item | State |
|---|---|
| Route | `/gmud` |
| Plugin | `@internal/plugin-change-management` |
| API | `ChangeManagementApi` + `MockChangeManagementApi` (mock ID `MOCK-CHG-000001`) |
| Domain model | Generic `CreateChangeRequest` — `targetRef`, `classification`, `requestedWindow`, `risk`, `rollbackPlan`, `evidence` |
| UI | Four numbered form sections + informational right rail (Fluxo da Mudança) |
| Backend | **Not implemented** — F2 gated |

### Normative references

- UI contract: [`gmud-create-screen.md`](../ui/gmud-create-screen.md)
- Architecture decisions: [`docs/adr/`](../adr/README.md) (ADR-001 through ADR-005 on `main`)
- F1.4 handoff detail: [`implementation-progress.md`](./implementation-progress.md)

### Visual baseline

- F1.2 before baseline: [`gmud-create-f1.2-after.png`](../ui/screenshots/gmud-create-f1.2-after.png)
- F1.3+ after capture: manual — see [`screenshots/README.md`](../ui/screenshots/README.md)

## Review gate — STOP before F2

F1.4 integrity cleanup is **complete**. Do **not** begin F2 backend (`ChangeManagementService`, ITSM providers, persistence) until architecture stakeholders review:

1. Consolidated ADR set (ADR-001 through ADR-005 on `main`)
2. F1.4 normative UI contract in [`docs/ui/gmud-create-screen.md`](../ui/gmud-create-screen.md)
3. Deviations between ADO implementation and bridge ADRs (if any)

## Source-of-truth rules

| Question | Authority |
|---|---|
| What is implemented? | ADO `platform-devops-developer-portal` source code |
| What should be implemented? | Bridge ADRs and normative contracts in this repository (`main`) |
| Divergence | Report as deviation in `implementation-progress.md` — do not silently alter architecture docs to match code |

## ADO implementation reference

| Item | Value |
|---|---|
| Branch | `feat/ado-repo-governance` |
| Implementation commit | `52e01ca` (F1.4 integrity cleanup) |
| Bridge handoff commit | `a0c2409` |

## Superseded references

| Item | Status |
|---|---|
| Branch `docs/architecture-decisions-mvp` | Superseded by `main` — historical baseline only |
| GitHub mirror `diegofernandes-dev/platform-devops-developer-portal` | Deprecated accidental mirror — do not use for development |
