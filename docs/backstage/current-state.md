# Backstage IDP — current state snapshot

> **Bridge repository:** `diegofernandes-dev/poc-teams-approval`  
> **Canonical architectural branch:** `main` (branch `docs/architecture-decisions-mvp` superseded as of F1.4)  
> **Implementation repository (ADO):** `platform-devops-developer-portal`  
> **Active branch:** `feat/ado-repo-governance`  
> **Last updated:** 2026-08-31 (F2.1.1 idempotency recovery checkpoint)

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

### Backend (F2.1 — durable Model C persistence)

| Item | State |
|---|---|
| Plugin | `change-management` (`createBackendPlugin`) |
| Routes | `POST /api/change-management/changes` · `GET /api/change-management/changes/:changeId` |
| Service | `ChangeManagementService` |
| Platform index | `ChangeIndexRepository` → `change_index` table (identity, routing, audit snapshot) |
| Idempotency | `IdempotencyRepository` → `change_idempotency` table (platform-owned) |
| Sequence | `DatabaseChangeIdGenerator` → `change_id_sequences` table |
| Provider registry | `ProviderRegistry` — immutable `providerKey` routing per change |
| Provider | `IChangeManagementProvider` → `DevelopmentProvider` (`providerKey: development`, non-production) |
| Persistence | SQLite (dev) / Postgres (prod) via `coreServices.database` |
| Frontend wiring | **Not connected** — `/gmud` still submits to mock |
| RBAC | `change-management.change.create` / `.read` (unchanged) |
| Canonical backend contract | [ADR-006](../adr/ADR-006-change-management-backend-contract.md) |
| Record authority | [ADR-007](../adr/ADR-007-change-record-authority.md) — Model C |

#### Backend capabilities (F2.1 + F2.1.1)

- Durable platform canonical index with immutable `providerKey`, `externalId`, creation-time snapshot
- GET routes: index → stored `providerKey` → provider adapter (not global config)
- Durable idempotency with early reserve + atomic finalize (`Idempotency-Key` header)
- **F2.1.1:** crash-safe idempotency recovery — `pending`/`completed` state machine; synchronous resume on retry; index-as-recovery-evidence; provider idempotent `create` by `changeId`
- Durable platform-owned `changeId` sequence (`CHG-{YYYY}-{seq}`)
- `DevelopmentProvider` operational records in `development_change_records` (logically isolated)
- Fail-closed provider errors (503); unfinalized index rows not visible on GET
- Degraded read: **503** when provider unavailable (snapshot exists but not returned — explicit degraded response deferred)

#### Explicitly not implemented (F2.1)

- Real ITSM providers (SharePoint, Jira, ServiceNow)
- Frontend → backend integration
- List/search, detail page, attachments, workflow engine
- Azure DevOps / pipeline / deployment correlation

### Architecture review outcome (F2.1 checkpoint)

| Decision | Outcome |
|---|---|
| Model | **Model C retained** — `ChangeIndexRepository` + `IChangeManagementProvider` (not Model B monolith) |
| `providerKey` | Immutable per change; persisted in `change_index` |
| Historical routing | GET uses stored `providerKey`, not current global config |
| Orphan record handling | Log `change.create.orphan` (with `idempotencyKey`) + synchronous retry reconciliation; no background worker |
| Idempotency recovery | **F2.1.1:** pending resume, finalized-index heal, provider idempotent create — see §13 |
| Frontend wiring | **Deferred** — conditional GO after F2.1.1 architecture review |

### Normative references

- UI contract: [`gmud-create-screen.md`](../ui/gmud-create-screen.md) (F1.4 — frontend unchanged)
- Architecture decisions: [`docs/adr/`](../adr/README.md) (ADR-001 through ADR-007 on `main`)
- Backend contract: [ADR-006](../adr/ADR-006-change-management-backend-contract.md)
- Record authority: [ADR-007](../adr/ADR-007-change-record-authority.md)
- Handoff detail: [`implementation-progress.md`](./implementation-progress.md) §9–§10

### Visual baseline

- F1.2 before baseline: [`gmud-create-f1.2-after.png`](../ui/screenshots/gmud-create-f1.2-after.png)
- F1.3+ after capture: manual — see [`screenshots/README.md`](../ui/screenshots/README.md)

## Review gate — F2.1.1 idempotency recovery checkpoint (awaiting architecture review)

F2.1.1 closes crash/retry reliability gaps before frontend wiring. Frontend remains mock-backed.

**STOP** before SharePoint/Jira/ServiceNow. **STOP** frontend wiring until F2.1.1 review completes.

See [`implementation-progress.md`](./implementation-progress.md) §13 for checkpoint detail, tests, and deviations.

## Review gate — F2.1 backend checkpoint (passed — superseded by F2.1.1 gate)

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
| **F2.1 commit** | **`0dc3ed4`** (durable Model C backend persistence) |
| **F2.1.1 commit** | **`ed6810b`** (idempotency recovery — see §13) |
| Bridge F2.0 handoff | `4ec7292` · `317b821` · `047dcc6` |
| **Bridge architecture review** | **`57613ab`** (ADR-007 + §10) |
| **Bridge F2.1 handoff** | **`196949c`** (ADO SHA `0dc3ed4` recorded) |
| **Bridge F2.1.1 handoff** | **`a65e1ed`** · **`44729aa`** · **`afaaaf6`** (ADO SHA `ed6810b`) |

## Superseded references

| Item | Status |
|---|---|
| Branch `docs/architecture-decisions-mvp` | Superseded by `main` — historical baseline only |
| GitHub mirror `diegofernandes-dev/platform-devops-developer-portal` | Deprecated accidental mirror — do not use for development |
| F1.4 "STOP before F2" gate | **Passed** — F2.0 delivered |
| F2.0 "STOP before F2.1" gate | **Passed** — ADR-007 accepted 2026-08-31; ADO realigned to `b2bed17` |
