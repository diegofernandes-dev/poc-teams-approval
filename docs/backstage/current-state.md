# Backstage IDP — current state snapshot

> **Bridge repository:** `diegofernandes-dev/poc-teams-approval`  
> **Canonical architectural branch:** `main` (branch `docs/architecture-decisions-mvp` superseded as of F1.4)  
> **Implementation repository (ADO):** `platform-devops-developer-portal`  
> **Active branch:** `feat/ado-repo-governance`  
> **Last updated:** 2026-09-01 (F2.2 — My Changes List + Change Detail)

## Stack

| Item | Value |
|---|---|
| Platform | Backstage 1.51.0 |
| Auth | Microsoft Entra ID + MS Graph catalog ingestion |
| RBAC | Community RBAC + ownership on Systems |
| Azure DevOps | Project access, repo governance, pipeline integration |
| TechDocs | AWS S3 (production path) |

## GMUD change management

### Frontend (F2.2)

| Item | State |
|---|---|
| Route | `/gmud` (Minhas GMUDs) · `/gmud/new` (create, moved from `/gmud`) · `/gmud/:changeId` (detail) — one `page:change-management` extension, nested `<Routes>` via `GmudRouter` |
| Plugin | `@internal/plugin-change-management` |
| API (client) | `ChangeManagementApi` — real `ChangeManagementClient` wired to the backend (`createChangeRequest`, `listChanges`, `getChange`); `MockChangeManagementApi` kept for tests only |
| Domain model (frontend) | `Change` / `ChangeSummary` mirrored from backend `types.ts`; `CreateChangeHttpBody` — `targetRef`, `classification`, `requestedWindow`, `risk`, `rollbackPlan`, `evidence`, `executionPlan` |
| UI | Create: five numbered form sections (execution plan added, see F2.1.2 backfill) + informational right rail. List: compact `Table` (Minhas GMUDs). Detail: read-only `InfoCard`s, ordered execution plan. Post-create: Ver GMUD / Criar outra GMUD / Voltar para Minhas GMUDs |

### Backend (F2.2)

| Item | State |
|---|---|
| Plugin | `change-management` (`createBackendPlugin`) |
| Routes | `POST /api/change-management/changes` · `GET /api/change-management/changes` · `GET /api/change-management/changes/:changeId` |
| Service | `ChangeManagementService` — `createChange`, `listChanges`, `getChange` |
| Provider | `IChangeManagementProvider` → `DevelopmentProvider` (durable, non-production; `development_change_records` table) |
| Persistence | Durable — `change_index` (platform canonical index) + `development_change_records`, real knex migrations, better-sqlite3 dev DB |
| Frontend wiring | **Connected** — create, list, and detail all call the real backend |
| RBAC | `change-management.change.create` / `.read` (contributor, template_executor, platform_admin) — `.read` also gates `GET /changes` |
| Canonical backend contract | [ADR-006](../adr/ADR-006-change-management-backend-contract.md) (HTTP contract + list read scope) |
| Record authority decision | [ADR-007](../adr/ADR-007-change-record-authority.md) — Model C (hybrid index + provider record) + discovery/detail clarification |

#### Backend capabilities (F2.2)

- Create change with server-trusted `requestedBy`, catalog-derived `ownerRef`/`systemRef` (creation snapshots), multi-activity execution plan (1–20 activities, `responsibleRef` validated as a Catalog Group)
- Service-owned `changeId` (`CHG-{YYYY}-{seq}`, durable sequence); fail-closed on provider error
- Minimal status: `submitted` only
- `Idempotency-Key` header — durable, crash-safe (reserve/claim, orphan recovery)
- Timezone normalization via `changeManagement.defaultTimezone`
- **New in F2.2:** `GET /changes` — actor-scoped discovery from the canonical index, same authorization predicate as `GET /:changeId`, zero provider calls; `ChangeSummary` projection with no provider metadata

#### Explicitly not implemented (F2.2)

- Real ITSM providers (SharePoint, Jira, ServiceNow)
- Azure DevOps / pipeline / deployment correlation
- Approvals, workflow, activity/lifecycle statuses beyond `submitted`
- Teams, CAB scheduling
- Evidence upload, editing, deletion
- Team-wide / enterprise-wide search, filters, sorting frameworks (deferred to F3)
- ADR-008 for the multi-activity execution plan domain (open documentation gap — see `implementation-progress.md` §12)

### Architecture review outcome (F2.0 checkpoint, still current)

| Decision | Outcome |
|---|---|
| Canonical record ownership | **Model C** — platform canonical index + ITSM provider operational record |
| Provider replaceability | API/frontend contract stable; multi-provider read routing via immutable `providerKey` |
| `changeId` ownership | Platform (`ChangeManagementService` / `changeIdGenerator`) — confirmed in ADO `b2bed17`, durable as of F2.1 |
| Idempotency ownership | Platform service store — confirmed in ADO `b2bed17`, durable/crash-safe as of F2.1.1 |
| Catalog `ownerRef`/`systemRef` | Creation-time snapshots — not live catalog refs on GET or list |
| List vs. detail authority (F2.2) | List = index snapshot (discovery only); detail = provider-authoritative, unchanged routing |
| F2.1 readiness | **Conditional GO** — ADR-007 accepted 2026-08-31; ADO code realigned to `b2bed17` before F2.1 coding |

See [`implementation-progress.md`](./implementation-progress.md) §10–§13 for full review detail, ADO deviations, the F2.1–F2.1.3 backfill, and F2.2.

### Normative references

- UI contracts: [`gmud-create-screen.md`](../ui/gmud-create-screen.md) · [`gmud-my-changes-screen.md`](../ui/gmud-my-changes-screen.md) · [`gmud-detail-screen.md`](../ui/gmud-detail-screen.md)
- Architecture decisions: [`docs/adr/`](../adr/README.md) (ADR-001 through ADR-007 on `main`; **ADR-008 does not exist** — see gap note above)
- Backend contract: [ADR-006](../adr/ADR-006-change-management-backend-contract.md)
- Record authority: [ADR-007](../adr/ADR-007-change-record-authority.md)
- Handoff detail: [`implementation-progress.md`](./implementation-progress.md) §9–§13

### Visual baseline

- F1.2 before baseline: [`gmud-create-f1.2-after.png`](../ui/screenshots/gmud-create-f1.2-after.png)
- F1.3+ after capture: manual — see [`screenshots/README.md`](../ui/screenshots/README.md)

## Review gate — conditional GO for F2.1 ADO implementation

F2.0 backend **contract scaffold** and **architecture review** are **complete**.
Stakeholders accepted Model C on **2026-08-31**:

1. [ADR-007](../adr/ADR-007-change-record-authority.md) — record authority (Model C)
2. [ADR-006](../adr/ADR-006-change-management-backend-contract.md) — clarifications (idempotency, GET routing, snapshots)
3. [ADR-003](../adr/ADR-003-provider-agnostic-change-management.md) — narrowed replaceability guarantee

**ADO prerequisite:** uncommitted Model B drift (provider-less `ChangeRepository`) was
identified and **reverted** to `b2bed17` before F2.1 coding. See
[`implementation-progress.md`](./implementation-progress.md) §11.

F2.1 may proceed per ADR-007 scope (platform canonical index, `DevelopmentProvider`,
durable idempotency/sequence). **STOP** before SharePoint/Jira/ServiceNow and before
frontend wiring until the F2.1 backend checkpoint is reviewed.

## Review gate — F2.2 complete, STOP before F3

F2.1–F2.1.3 landed on ADO without an intervening bridge handoff — including the
frontend wiring §"STOP before F2.1 gate" above asked to hold for review. That gap is
recorded and backfilled in [`implementation-progress.md`](./implementation-progress.md)
§12, not silently absorbed. **F2.2** (this checkpoint) adds My Changes List + Change
Detail on top of that baseline:

1. `GET /changes` — index-backed discovery, actor-scoped, zero provider calls
2. Minhas GMUDs list page + read-only detail page + post-create navigation
3. List/detail authorization proven identical by construction (`canReadChange` reused)
4. Real `DevelopmentProvider` persistence verified against a file-backed database (§13)

**STOP before F3** — approvals, workflow, activity lifecycle statuses, real ITSM
providers, Teams, CAB, Azure DevOps correlation, evidence upload, editing, and
team-wide/enterprise search all require a separate architecture review.

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
| F2.0 commit | `b2bed17` (backend contract scaffold) |
| F2.1 commit | `0dc3ed4` (durable canonical index + `DevelopmentProvider`) |
| F2.1.1 commit | `ed6810b` (idempotency recovery, crash-safe retry) |
| F2.1.2 commit | `5e4f30e` (multi-activity execution plan domain) |
| F2.1.3 commit | `75da44f` (execution plan wired to real backend) |
| **F2.2 commit** | **`0b9cb38`** (My Changes List + Change Detail) |
| Bridge F2.0 handoff | `4ec7292` · `317b821` · `047dcc6` |
| Bridge architecture review | `57613ab` (ADR-007 + §10) |
| **Bridge F2.2 handoff** | *(this checkpoint's commit — see `implementation-progress.md` §13)* |

## Superseded references

| Item | Status |
|---|---|
| Branch `docs/architecture-decisions-mvp` | Superseded by `main` — historical baseline only |
| GitHub mirror `diegofernandes-dev/platform-devops-developer-portal` | Deprecated accidental mirror — do not use for development |
| F1.4 "STOP before F2" gate | **Passed** — F2.0 delivered |
| F2.0 "STOP before F2.1" gate | **Passed** — ADR-007 accepted 2026-08-31; ADO realigned to `b2bed17` |
| F2.1 "STOP for review after frontend wiring" gate | **Missed** — wiring (`75da44f`) landed without a bridge review; backfilled in `implementation-progress.md` §12, not repeated for F2.2 |
| F2.2 "STOP before F3" gate | **Active** — do not begin approvals/workflow/real providers/Teams/CAB without review |
