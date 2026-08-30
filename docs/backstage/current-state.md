# Backstage IDP — current state snapshot

> **Bridge repository:** `diegofernandes-dev/poc-teams-approval`  
> **Implementation repository (ADO):** `platform-devops-developer-portal`  
> **Active branch:** `feat/ado-repo-governance`  
> **Last updated:** 2026-08-30 (F1.3 handoff)

## Stack

| Item | Value |
|---|---|
| Platform | Backstage 1.51.0 |
| Auth | Microsoft Entra ID + MS Graph catalog ingestion |
| RBAC | Community RBAC + ownership on Systems |
| Azure DevOps | Project access, repo governance, pipeline integration |
| TechDocs | AWS S3 (production path) |

## GMUD change management (F1.3)

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
- Architecture decision: [`ADR-002-backstage-change-onramp.md`](../adr/ADR-002-backstage-change-onramp.md)
- F1.3 handoff detail: [`implementation-progress.md`](./implementation-progress.md)

### Visual baseline

- F1.2 before baseline: [`gmud-create-f1.2-after.png`](../ui/screenshots/gmud-create-f1.2-after.png)
- F1.3 after capture: manual — see [`screenshots/README.md`](../ui/screenshots/README.md)

## Review gate — STOP before F2

F1.3 frontend slice is **complete**. Do **not** begin F2 backend (`ChangeManagementService`, ITSM providers, persistence) until architecture stakeholders review:

1. F1.3 `CreateChangeRequest` domain model
2. Normative UI contract in [`docs/ui/gmud-create-screen.md`](../ui/gmud-create-screen.md)
3. Deviations between ADO implementation and bridge ADRs (if any)

## Source-of-truth rules

| Question | Authority |
|---|---|
| What is implemented? | ADO `platform-devops-developer-portal` source code |
| What should be implemented? | Bridge ADRs and normative contracts in this repository |
| Divergence | Report as deviation in `implementation-progress.md` — do not silently alter architecture docs to match code |

## ADO implementation reference

| Item | Value |
|---|---|
| Branch | `feat/ado-repo-governance` |
| Implementation commit | `087903f` (F1.3 plugin + doc cleanup) |
| Prior F1.3 commit | `3cd285a` |
| Bridge handoff commit | `fdae3f3` |
