# Backstage IDP — current state snapshot

> **Bridge repository:** `diegofernandes-dev/poc-teams-approval`  
> **Canonical architectural branch:** `main` (branch `docs/architecture-decisions-mvp` superseded as of F1.4)  
> **Implementation repository (ADO):** `platform-devops-developer-portal`  
> **Active branch:** `feat/ado-repo-governance`  
> **Last updated:** 2026-08-30 (F2.0 architecture review)

## Stack

| Item | Value |
|---|---|
| Platform | Backstage 1.51.0 |
| Auth | Microsoft Entra ID + MS Graph catalog ingestion |
| RBAC | Community RBAC + ownership on Systems |
| Azure DevOps | Project access, repo governance, pipeline integration |
| TechDocs | AWS S3 (production path) |

## GMUD change management

### Frontend (F1.4 — unchanged in F2.0)

| Item | State |
|---|---|
| Route | `/gmud` |
| Plugin | `@internal/plugin-change-management` |
| API (client) | `ChangeManagementApi` + `MockChangeManagementApi` (still mock — **not wired to backend**) |
| Domain model (frontend) | `CreateChangeRequest` — `targetRef`, `classification`, `requestedWindow`, `risk`, `rollbackPlan`, `evidence` |
| UI | Four numbered form sections + informational right rail (Fluxo da Mudança) |

### Backend (F2.0 — contract scaffold)

| Item | State |
|---|---|
| Plugin | `change-management` (`createBackendPlugin`) |
| Routes | `POST /api/change-management/changes` · `GET /api/change-management/changes/:changeId` |
| Service | `ChangeManagementService` |
| Provider | `IChangeManagementProvider` → `FakeChangeManagementProvider` (in-memory, non-production) |
| Persistence | **None durable** — fake provider only; **no platform canonical index yet** |
| Frontend wiring | **Not connected** — `/gmud` still submits to mock |
| RBAC | `change-management.change.create` / `.read` (contributor, template_executor, platform_admin) |
| Canonical backend contract | [ADR-006](../adr/ADR-006-change-management-backend-contract.md) |
| Record authority decision | [ADR-007](../adr/ADR-007-change-record-authority.md) — Model C (hybrid index + provider record) |

#### Backend capabilities (F2.0)

- Create change with server-trusted `requestedBy`, catalog-derived `ownerRef`/`systemRef` (creation snapshots)
- Service-owned `changeId` (`CHG-{YYYY}-{seq}` via `changeIdGenerator.ts`); fail-closed on provider error
- Minimal status: `submitted` only
- Optional `Idempotency-Key` header (service-owned in-memory store)
- Timezone normalization via `changeManagement.defaultTimezone`

#### Explicitly not implemented (F2.0)

- Platform canonical index / durable persistence
- Real ITSM providers (SharePoint, Jira, ServiceNow)
- Azure DevOps / pipeline / deployment correlation
- Frontend → backend integration
- List/search, detail page, attachments, workflow engine

### Architecture review outcome (F2.0 checkpoint)

| Decision | Outcome |
|---|---|
| Canonical record ownership | **Model C** — platform canonical index + ITSM provider operational record |
| Provider replaceability | API/frontend contract stable; multi-provider read routing via immutable `providerKey` |
| `changeId` ownership | Platform (`ChangeManagementService` / `changeIdGenerator`) — confirmed in ADO `b2bed17` |
| Idempotency ownership | Platform service store — confirmed in ADO `b2bed17` |
| Catalog `ownerRef`/`systemRef` | Creation-time snapshots — not live catalog refs on GET |
| F2.1 readiness | **Conditional GO** — architecture documented; await stakeholder acceptance before ADO F2.1 coding |

See [`implementation-progress.md`](./implementation-progress.md) §10 for full review detail and ADO deviations.

### Normative references

- UI contract: [`gmud-create-screen.md`](../ui/gmud-create-screen.md) (F1.4 — frontend unchanged)
- Architecture decisions: [`docs/adr/`](../adr/README.md) (ADR-001 through ADR-007 on `main`)
- Backend contract: [ADR-006](../adr/ADR-006-change-management-backend-contract.md)
- Record authority: [ADR-007](../adr/ADR-007-change-record-authority.md)
- Handoff detail: [`implementation-progress.md`](./implementation-progress.md) §9–§10

### Visual baseline

- F1.2 before baseline: [`gmud-create-f1.2-after.png`](../ui/screenshots/gmud-create-f1.2-after.png)
- F1.3+ after capture: manual — see [`screenshots/README.md`](../ui/screenshots/README.md)

## Review gate — STOP before F2.1 ADO implementation

F2.0 backend **contract scaffold** and **architecture review** are **complete**.

**Do not begin F2.1** (durable platform index, frontend wiring, development provider) until architecture stakeholders **accept**:

1. [ADR-007](../adr/ADR-007-change-record-authority.md) — record authority (Model C)
2. [ADR-006](../adr/ADR-006-change-management-backend-contract.md) — clarifications (idempotency, GET routing, snapshots)
3. [ADR-003](../adr/ADR-003-provider-agnostic-change-management.md) — narrowed replaceability guarantee

F2.1 is **not approved** until explicit acceptance is recorded.

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
| F1.4 commit | `52e01ca` |
| **F2.0 commit** | **`b2bed17`** (backend contract scaffold) |
| Bridge F2.0 handoff | `4ec7292` · `317b821` · `047dcc6` |
| **Bridge architecture review** | **pending commit** (ADR-007 + §10) |

## Superseded references

| Item | Status |
|---|---|
| Branch `docs/architecture-decisions-mvp` | Superseded by `main` — historical baseline only |
| GitHub mirror `diegofernandes-dev/platform-devops-developer-portal` | Deprecated accidental mirror — do not use for development |
| F1.4 "STOP before F2" gate | **Passed** — F2.0 delivered |
| F2.0 "STOP before F2.1" gate | **Architecture review complete** — awaiting acceptance before ADO F2.1 |
