# Backstage IDP — current state snapshot

> **Bridge repository:** `diegofernandes-dev/poc-teams-approval`  
> **Canonical architectural branch:** `main` (branch `docs/architecture-decisions-mvp` superseded as of F1.4)  
> **Implementation repository (ADO):** `platform-devops-developer-portal`  
> **Active branch:** `feat/ado-repo-governance`  
> **Last updated:** 2026-09-01 (F3.0 — Change Authorization Architecture, proposed)

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
| API (client) | `ChangeManagementApi` → `ChangeManagementClient` via Backstage discovery/fetch (`createChangeRequest`, `listChanges`, `getChange`); mock retained for tests/fixtures only |
| Domain model (frontend) | `Change` / `ChangeSummary` mirrored from backend `types.ts`; `CreateChangeHttpBody` — `targetRef`, `classification`, `requestedWindow`, `risk`, `rollbackPlan`, `evidence`, `executionPlan` (required, ordered, 1–20 activities per [ADR-008](../adr/ADR-008-multi-activity-change-execution-plan.md)) |
| UI | Create: five numbered sections, Catalog Group executor + optional Component target per activity. List: compact `Table` (Minhas GMUDs). Detail: read-only `InfoCard`s, ordered execution plan. Post-create: Ver GMUD / Criar outra GMUD / Voltar para Minhas GMUDs |

### Backend (F2.2)

| Item | State |
|---|---|
| Plugin | `change-management` (`createBackendPlugin`) |
| Routes | `POST /api/change-management/changes` · `GET /api/change-management/changes` · `GET /api/change-management/changes/:changeId` |
| Service | `ChangeManagementService` — `createChange`, `listChanges`, `getChange` |
| Platform index | `ChangeIndexRepository` → `change_index` table (identity, routing, audit snapshot incl. `executionPlan`) |
| Idempotency | `IdempotencyRepository` → `change_idempotency` table (platform-owned, crash-safe recovery) |
| Sequence | `DatabaseChangeIdGenerator` → `change_id_sequences` table |
| Provider registry | `ProviderRegistry` — immutable `providerKey` routing per change |
| Provider | `IChangeManagementProvider` → `DevelopmentProvider` (`providerKey: development`, non-production; `development_change_records` table) |
| Persistence | SQLite (dev) / Postgres (prod) via `coreServices.database`, real knex migrations |
| Frontend wiring | **Connected** — create, list, and detail all call the real backend |
| RBAC | `change-management.change.create` / `.read` (contributor, template_executor, platform_admin) — `.read` also gates `GET /changes` |
| Canonical backend contract | [ADR-006](../adr/ADR-006-change-management-backend-contract.md) (HTTP contract + participant read scope) |
| Record authority | [ADR-007](../adr/ADR-007-change-record-authority.md) — Model C (hybrid index + provider record) + discovery/detail clarification |
| Execution plan domain | [ADR-008](../adr/ADR-008-multi-activity-change-execution-plan.md) — F2.1.2, read-visibility clause superseded by F2.2.1 |

#### Backend capabilities (F2.2.1)

- Durable platform canonical index with immutable `providerKey`, `externalId`, creation-time snapshot
- GET routes: index → stored `providerKey` → provider adapter (not global config)
- Durable idempotency with early reserve + atomic finalize (`Idempotency-Key` header); crash-safe recovery (`pending`/`completed` state machine, synchronous resume on retry, provider idempotent `create` by `changeId`)
- Durable platform-owned `changeId` sequence (`CHG-{YYYY}-{seq}`)
- `DevelopmentProvider` operational records in `development_change_records` (logically isolated)
- Fail-closed provider errors (503); unfinalized index rows not visible on GET or list
- `executionPlan` / `ExecutionActivity` on canonical `Change`; catalog-validated `responsibleRef` (Group) and optional activity `targetRef`
- Actor-scoped idempotency: `(operation, requested_by, idempotency_key)` — different actors may independently reuse the same key
- `GET /changes` (F2.2) — participant-scoped discovery from the canonical index, identical authorization predicate as `GET /:changeId`, zero provider calls; `ChangeSummary` projection with no provider metadata
- **New in F2.2.1:** `canReadChange` (shared by list and detail) grants read to any actor whose `ownershipEntityRefs` includes `change.ownerRef` **or any** `executionPlan.activities[].responsibleRef` — the **change participant** read policy. Read only; no ownership/approval/CAB/edit/execution authority. Backed by a derived, non-authoritative discovery index (`change_index_activity_participants`), backfilled by migration; `canReadChange` against the immutable snapshot remains the sole authorization truth.

#### Explicitly not implemented (F2.2.1)

- Real ITSM providers (SharePoint, Jira, ServiceNow)
- Azure DevOps / pipeline / deployment correlation
- Approvals, workflow, activity/lifecycle statuses beyond `submitted`
- Teams, CAB scheduling
- Evidence upload, editing, deletion
- Team-wide / enterprise-wide search, filters, sorting frameworks (deferred to F3)
- New governance roles (`change.read.all`, Change Manager, Auditor) (deferred to F3)

### Architecture review outcome (cumulative through F2.2.1)

| Decision | Outcome |
|---|---|
| Canonical record ownership | **Model C** — platform canonical index + ITSM provider operational record |
| Provider replaceability | API/frontend contract stable; multi-provider read routing via immutable `providerKey` |
| `changeId` ownership | Platform (`ChangeManagementService` / `changeIdGenerator`) — confirmed in ADO `b2bed17`, durable as of F2.1 |
| Idempotency ownership | Platform service store — confirmed in ADO `b2bed17`, durable/crash-safe as of F2.1.1 |
| Orphan record handling | Log `change.create.orphan` (with `idempotencyKey`) + synchronous retry reconciliation; no background worker |
| Execution plan domain | `ExecutionPlan`/`ExecutionActivity` accepted per ADR-008 (F2.1.2) — provider-neutral, no per-activity status |
| Catalog `ownerRef`/`systemRef` | Creation-time snapshots — not live catalog refs on GET or list |
| Frontend wiring | Complete as of F2.1.3 — real create client, no create-time GET |
| List vs. detail authority (F2.2) | List = index snapshot (discovery only); detail = provider-authoritative, unchanged routing |
| Read visibility (F2.2.1) | **Change participant** policy — `platform_admin` OR requester OR `ownerRef` team OR any activity `responsibleRef` team; read only, no other authority; supersedes ADR-008's F2.1.2 "responsibleRef grants no read access" clause |

### F3.0 proposed authorization architecture (documentation only)

[ADR-009](../adr/ADR-009-change-authorization-model.md) defines the proposed target
without changing the F2.2.1 implementation:

| Concern | Proposed decision |
|---|---|
| Business authorization authority | Platform Change Management authorization ledger, not Azure DevOps or an ITSM provider |
| Policy | Deterministic, immutable, versioned policy evaluated at submission |
| Requirements | Effective, snapshotted, provider-neutral; policy-generated plus additive mandatory requirements |
| Decisions | Immutable/append-only `approved` or `rejected` human/governance facts |
| Principal identity | Configured selectors resolved to snapshotted platform principal refs; no titles/names in domain |
| CAB | One collective authority decision by default; recorded by an authorized operator/delegate |
| Emergency | Multiple generic pre-execution approvals plus post-execution CAB retrospective |
| Authorization vs execution | Authorization = governance complete; executable now additionally requires lifecycle, window, target correlation, and no hold |
| Teams | Future individual-decision interaction channel; never system of record |
| Backstage | Preferred future CAB Workbench UI; backend authorization ledger remains authoritative |
| Pipelines | Future provider-neutral execution-eligibility consumers; no ADO object in canonical Change |
| DevOps | Policy/control/integration/observability/exception owner; absent from happy-path per-deploy approval |
| Model C | Retained; bounded platform authorization ledger sits beside the index while provider owns operational GMUD detail |

**Gate:** architecture is ready for stakeholder review but **NO-GO for F3.1
implementation planning** until ADR-009's nine must-decide product/governance items
are resolved and the ADR is accepted. No application code, route, migration, Teams,
CAB UI, pipeline enforcement, or real provider was introduced in F3.0.

See [`implementation-progress.md`](./implementation-progress.md) §12–§18 for full checkpoint detail (F2.1, F2.1.1, F2.1.2, F2.1.3, F2.2, F2.2.1, F3.0 architecture).

### Normative references

- UI contracts: [`gmud-create-screen.md`](../ui/gmud-create-screen.md) · [`gmud-my-changes-screen.md`](../ui/gmud-my-changes-screen.md) · [`gmud-detail-screen.md`](../ui/gmud-detail-screen.md)
- Architecture decisions: [`docs/adr/`](../adr/README.md) (ADR-001 through ADR-008 on `main`)
- Backend contract: [ADR-006](../adr/ADR-006-change-management-backend-contract.md)
- Record authority: [ADR-007](../adr/ADR-007-change-record-authority.md)
- Execution plan domain: [ADR-008](../adr/ADR-008-multi-activity-change-execution-plan.md)
- Authorization model: [ADR-009](../adr/ADR-009-change-authorization-model.md) — proposed F3.0 architecture; no implementation
- Handoff detail: [`implementation-progress.md`](./implementation-progress.md) §9–§18

### Visual baseline

- F1.2 before baseline: [`gmud-create-f1.2-after.png`](../ui/screenshots/gmud-create-f1.2-after.png)
- F1.3+ after capture: manual — see [`screenshots/README.md`](../ui/screenshots/README.md)

## Review gate — F2.2.1 complete, STOP before F3

**F2.2.1** closes the domain contradiction ADR-008 (F2.1.2) left open: an activity
`responsibleRef` team could be assigned execution responsibility without being able
to read the GMUD that assigns it. Adds the **change participant** read policy on
top of the F2.2 discovery baseline:

1. `canReadChange` (shared by list and detail — unchanged sharing from F2.2) gains
   a fourth clause: any `executionPlan.activities[].responsibleRef` membership grants
   read, alongside `platform_admin` / requester / `ownerRef` team
2. New derived, non-authoritative discovery index `change_index_activity_participants`
   (backfilled by migration) narrows the list query's SQL candidates; the snapshot
   predicate remains the sole authorization truth
3. List/detail authorization proven identical for every participant class, including
   activity-responsible teams, by construction and by test
4. Read only — `responsibleRef` grants no ownership, approval, CAB, edit, or execution
   authority; ADR-008's F2.1.2 decision text is preserved with a superseded-by note,
   not rewritten
5. No workflow, approvals, activity status, Teams, CAB, new governance roles, or
   enterprise-wide search introduced

**STOP before F3** — approvals, workflow, activity lifecycle statuses, real ITSM
providers, Teams, CAB, Azure DevOps correlation, evidence upload, editing, new
governance roles (`change.read.all` / Change Manager / Auditor), and team-wide/
enterprise search all require a separate architecture review.

See [`implementation-progress.md`](./implementation-progress.md) §17 for checkpoint
detail.

## Review gate — F2.2 complete, STOP before F3 (superseded by F2.2.1 gate above)

**F2.2** adds My Changes List + Change Detail on top of the F2.1.3 real-backend
baseline (create → discover → open → read):

1. `GET /changes` — index-backed discovery, actor-scoped, zero provider calls
2. Minhas GMUDs list page + read-only detail page + post-create navigation
3. List/detail authorization proven identical by construction (`canReadChange` reused)
4. Real `DevelopmentProvider` persistence verified against a file-backed database (§16)

**STOP before F3** — approvals, workflow, activity lifecycle statuses, real ITSM
providers, Teams, CAB, Azure DevOps correlation, evidence upload, editing, and
team-wide/enterprise search all require a separate architecture review.

See [`implementation-progress.md`](./implementation-progress.md) §16 for checkpoint
detail, including a corrected process deviation from this checkpoint's own handoff
(a stale local bridge clone briefly produced a duplicate F2.1–F2.1.3 summary and an
incorrect "ADR-008 does not exist" claim — reconciled via merge before this commit;
no architectural impact).

## Review gate — F2.1.3 frontend create checkpoint (passed)

The `/gmud` form submits its five-section, ordered execution plan through the real
`ChangeManagementApi` client. Success is confirmed from the POST response and submitted
form snapshot; no GET, detail route, provider metadata, or automatic HTTP retry was added
in F2.1.3 (detail/GET was added subsequently, in F2.2).

All required frontend/backend tests, lint, build, and diff checks passed. **GO** was
given for the authenticated browser functional review.

See [`implementation-progress.md`](./implementation-progress.md) §15 for checkpoint detail.

## Review gate — F2.1.2 multi-activity execution plan checkpoint (passed)

F2.1.2 backend delivered: `ExecutionPlan` / `ExecutionActivity` on canonical `Change`,
index snapshot, catalog validation, per [ADR-008](../adr/ADR-008-multi-activity-change-execution-plan.md).

See [`implementation-progress.md`](./implementation-progress.md) §14 for checkpoint detail.

## Review gate — F2.1.1 idempotency recovery checkpoint (passed)

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
| F2.0 commit | `b2bed17` (backend contract scaffold) |
| F2.1 commit | `0dc3ed4` (durable canonical index + `DevelopmentProvider`) |
| F2.1.1 commit | `ed6810b` (idempotency recovery, crash-safe retry) |
| F2.1.2 commit | `5e4f30e` (multi-activity execution plan domain) |
| F2.1.3 commit | `75da44fb46d308e23b1c987e2093636fa4811b92` (execution plan wired to real backend) |
| F2.2 commit | `0b9cb38` (My Changes List + Change Detail) |
| **F2.2.1 commit** | **`6e28611`** (participant read policy) |
| Bridge F2.0 handoff | `4ec7292` · `317b821` · `047dcc6` |
| Bridge architecture review | `57613ab` (ADR-007 + §10) |
| Bridge F2.1 handoff | `196949c` (ADO SHA `0dc3ed4` recorded) |
| Bridge F2.1.1 handoff | `a65e1ed` · `44729aa` · `afaaaf6` (ADO SHA `ed6810b`) |
| Bridge F2.1.2 handoff | `afb154b` · `75b4f38` (ADR-008; ADO SHA `5e4f30e`) |
| Bridge F2.1.3 handoff | `f5f131f` (ADO SHA `75da44fb46d308e23b1c987e2093636fa4811b92`) |
| Bridge F2.2 handoff | `83a2b79` (reconciliation merge, `main`) — first drafted as `424e615` before fetching `f5f131f` |
| **Bridge F2.2.1 handoff** | **`8990dad`** (ADO SHA `6e28611` recorded) |

## Superseded references

| Item | Status |
|---|---|
| Branch `docs/architecture-decisions-mvp` | Superseded by `main` — historical baseline only |
| GitHub mirror `diegofernandes-dev/platform-devops-developer-portal` | Deprecated accidental mirror — do not use for development |
| F1.4 "STOP before F2" gate | **Passed** — F2.0 delivered |
| F2.0 "STOP before F2.1" gate | **Passed** — ADR-007 accepted 2026-08-31; ADO realigned to `b2bed17` |
| F2.1.2 "STOP before F2.1.3" gate | **Passed** — see F2.1.3 checkpoint above |
| F2.1.3 "STOP before next slice" gate | **Passed** — GO given for functional review; F2.2 proceeded as the next slice |
| F2.2 "STOP before F3" gate | **Passed** — F2.2.1 proceeded as the next slice (authorization hardening, not F3) |
| ADR-008 "responsibleRef grants no read access" (F2.1.2) | Superseded for read visibility by ADR-006 "Participant read scope (F2.2.1)" — decision text preserved as historical record |
| F2.2.1 "STOP before F3" gate | **Passed for architecture-only F3.0** — no implementation authorization was granted |
| F3.0 architecture-only gate | **Active** — ADR-009 proposed; NO-GO for F3.1 planning until must-decide items are resolved and stakeholder acceptance is recorded |
