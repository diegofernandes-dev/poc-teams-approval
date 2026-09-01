# ADR-007 — Change record authority and persistence ownership

- Status: Accepted (F2.0 architecture review; stakeholder acceptance 2026-08-31)
- Date: 2026-08-30
- Related: [ADR-002](./ADR-002-backstage-change-onramp.md), [ADR-003](./ADR-003-provider-agnostic-change-management.md), [ADR-006](./ADR-006-change-management-backend-contract.md), [ADR-008](./ADR-008-multi-activity-change-execution-plan.md)

## Context

F2.0 (ADO commit `b2bed17`) delivered `ChangeManagementService`, `IChangeManagementProvider`, HTTP create/get routes, and an in-memory fake provider. The service owns canonical `changeId` generation and the domain schema, but GET reads delegate entirely to the provider and `ProviderReference` is not persisted at the platform layer.

Before F2.1 introduces durable persistence — an irreversible decision — the architecture must explicitly answer: **who owns the canonical GMUD record?**

Three models were evaluated:

| Model | Summary |
|---|---|
| **A — Provider-owned record** | ITSM stores the durable record; platform is facade/validation/auth |
| **B — Platform-owned canonical store** | Platform DB is the enterprise system of record; ITSM is projection |
| **C — Hybrid canonical index + provider record** | Platform stores identity, routing, audit snapshot; ITSM stores operational record |

## Decision

**Adopt Model C — Hybrid Canonical Index + Provider Record.**

The platform is **not** building a ServiceNow/Jira-like system inside Backstage. It owns domain authority, canonical identity, routing, idempotency, and a creation-time audit snapshot. External ITSM providers remain the operational record authority for workflow, attachments, and approval lifecycle in production.

### Authority terminology

| Term | Owner |
|---|---|
| **Domain authority** | Platform — canonical `Change` schema, validation, HTTP API contract |
| **Record authority (production)** | Configured ITSM provider — full operational GMUD lifecycle |
| **Record authority (F2.1 dev)** | `DevelopmentProvider` backed by platform DB — explicitly **non-production** |
| **Identity authority** | Platform — `changeId` generation and idempotency |
| **Approval authority** | Azure DevOps (where applicable) — per ADR-001; unchanged |
| **Execution authority** | Out of scope |

Backstage remains the developer onramp (ADR-002). It does **not** become the enterprise GMUD workflow database.

### Target architecture (F2.1+)

```text
Backstage GMUD frontend
        |
        v
POST/GET /api/change-management/changes
        |
        v
ChangeManagementService
        |
        +-- Platform Canonical Index (durable)
        |     changeId, providerKey, externalId, externalUrl
        |     creation snapshot (Change fields for auth/audit/degraded read)
        |     idempotency records
        |     changeId sequence
        |
        +-- IChangeManagementProvider (router)
              |
              +-- DevelopmentProvider     [F2.1 non-production]
              +-- SharePointProvider      [future/optional]
              +-- JiraProvider            [future/optional]
              +-- ServiceNowProvider      [future/optional]
```

### Platform canonical index (minimum durable fields)

| Field group | Fields | Purpose |
|---|---|---|
| Identity | `changeId` | Canonical public identifier |
| Routing | `providerKey` (immutable), `externalId`, `externalUrl` | Provider adapter selection |
| Audit snapshot | `requestedBy`, `targetRef`, `ownerRef`, `systemRef`, `title`, `summary`, `requestedWindow`, `risk`, `classification`, `rollbackPlan`, `executionPlan`, `status`, `createdAt` | Auth, audit, degraded read when provider unavailable — `executionPlan` per [ADR-008](./ADR-008-multi-activity-change-execution-plan.md) |
| Idempotency | key → `{ payloadHash, changeId, status }` | Duplicate POST semantics (provider-agnostic) |

The provider persists the full operational record (future workflow state, attachments, CAB fields). Those concerns are explicitly out of platform scope until future ADRs.

### Why not Model A (provider-owned record)

Pure provider-owned record cannot support provider routing after a global config change without a platform mapping table. F2.0 code effectively implements Model A without the index — architecturally incomplete. Immutable per-change `providerKey` must live on the platform regardless of where the full record is stored.

### Why not Model B (platform-owned canonical store)

Conflicts with ADR-002 ("Backstage does not become the GMUD record database"). Introduces retention, DR, schema migration, and ITSM sync obligations disproportionate to the onramp product goal. High risk of accidentally building an ITSM platform inside Backstage.

### Anti-pattern: monolithic `ChangeRepository` without provider layer

A `ChangeRepository` that stores the complete canonical `Change` and **replaces**
`IChangeManagementProvider` in the service persistence path implements **Model B**,
not Model C. Symptoms include:

- no `providerKey` / `externalId` routing metadata;
- GET reads only from the platform database;
- provider abstraction deleted from the active backend path.

F2.1 must retain `IChangeManagementProvider` (router + `DevelopmentProvider` at
minimum). The platform canonical index stores identity, routing, audit snapshot,
idempotency, and sequence — not a substitute for the provider contract. A
`DevelopmentProvider` may persist a full record in platform DB for non-production
convenience, but only behind the provider interface with an immutable
`providerKey` recorded in the index.

## Provider replaceability (precise guarantee)

**Guaranteed:**

- Frontend create form and public HTTP contract remain stable when the provider adapter changes
- New changes use the configured default provider
- Domain types do not embed SharePoint/Jira/ServiceNow fields
- Adding or swapping a provider requires backend adapter + config only — no frontend rewrite

**Not guaranteed:**

- Automatic survival of all historical records if an old provider is decommissioned
- Zero-downtime bulk migration between ITSM systems
- Identical workflow semantics across providers
- GET succeeding when both platform index and original provider are unavailable

**Replaceability = adapter swap for new traffic + indefinite multi-provider read routing via immutable `providerKey` per change.**

Provider-agnosticism does **not** mean "switch production ITSM tomorrow with zero migration."

## Historical record behavior after provider switch

**Decision: multi-provider coexistence indefinitely (Option C).**

Scenario: 2026 SharePoint stores `CHG-2026-000100..102`; 2027 platform config default provider = Jira.

| Request | Behavior |
|---|---|
| `GET CHG-2026-000100` | Platform index → `providerKey: sharepoint` → `SharePointProvider.get(externalId)` → 200 if reachable; else 503 or degraded read from platform snapshot |
| `GET CHG-2027-000001` | Index → `providerKey: jira` → `JiraProvider.get(...)` |
| `POST /changes` (2027) | Platform generates new `changeId` → `JiraProvider.create` → index updated |

**Rules:**

1. Each change has **immutable `providerKey`** assigned at create — never rewritten on global config change
2. Global `changeManagement.provider` config selects **default for new records only**
3. Provider registry retains **all providers needed for historical reads** concurrently
4. Bulk migration is an **optional operational project**, not an architecture prerequisite
5. Decommissioning a provider requires an explicit archival/migration runbook

## Transaction and failure boundary (F2.1 target)

**When is a GMUD considered created (client-visible)?**

Platform index persisted with finalized `providerReference` **and** idempotency finalized → **201**.

**Fail-closed on provider write failure:** **503**, no `changeId` returned (preserve F2.0 semantics).

**Ordering (F2.1):**

```text
validate → authorize → enrich
→ idempotency check/reserve
→ generate changeId
→ persist platform index (internal pending if needed)
→ provider.create
→ finalize index + idempotency (atomic)
→ 201
```

**Invariants:**

- An external provider record must **not** exist without a corresponding platform index row (target state)
- A platform index row must **not** be publicly visible or return `changeId` while provider write has failed
- Do **not** introduce public workflow statuses (`integration_failed`, `pending_sync`) in F2.1 — keep `submitted` only until a workflow ADR exists
- Do **not** build a distributed saga or workflow engine

## Catalog snapshot semantics

`ownerRef` and `systemRef` are **creation-time snapshots**, not live catalog references.

- Resolved once from Catalog at create via `targetRef`
- Stored inside the persisted `Change` / platform index snapshot
- Returned as-is on GET — no catalog re-fetch on read

A GMUD created when owner = Team A must not historically appear owned by Team B after catalog ownership changes.

## F2.0 implementation gaps (ADO `b2bed17`)

Documented for F2.1 remediation — not deviations from domain intent:

| Gap | F2.0 state | F2.1 target |
|---|---|---|
| Platform canonical index | Not implemented | Durable `ChangeRepository` |
| `ProviderReference` persistence | Discarded after create | Stored in platform index |
| GET routing | Direct `provider.get(changeId)` | Index lookup → provider adapter |
| Idempotency durability | In-memory service store | DB with unique key constraint |
| `changeId` sequence | In-memory service generator | Platform DB sequence |
| Idempotency ordering | Written after provider success | Reserve early; atomic finalize |
| Concurrent idempotency | No lock | DB unique constraint |

**Confirmed correct in F2.0 code:**

- `changeId` generated by `ChangeManagementService` / `changeIdGenerator.ts` — not by provider
- Idempotency owned by service (`idempotency.ts`) — not by fake provider
- Provider receives pre-assigned `changeId`; does not mint canonical IDs
- Fail-closed on provider error (503, ID not returned)

ADR-006 previously stated idempotency and sequence lived in `FakeChangeManagementProvider` — that was a documentation error; corrected in ADR-006 update.

## F2.1 scope (after architecture acceptance)

1. Platform canonical index (`ChangeRepository`) — SQLite or Postgres for dev
2. Durable `changeId` sequence (platform-owned)
3. Durable idempotency with unique key constraint
4. Persist `ProviderReference`; route GET via index
5. `DevelopmentProvider` — non-production; may store full record in platform DB for dev convenience
6. Wire frontend `ChangeManagementApi` to backend
7. Remove `requestedBy` / `ownerRef` / `systemRef` from frontend POST payload
8. **STOP** before SharePoint / Jira / ServiceNow

## Consequences

- Positive: Smallest durable persistence that enables routing, auth, audit, and degraded read without building an ITSM platform
- Positive: ITSM remains operational record authority in production
- Positive: Provider switch semantics are explicit and honest
- Trade-off: Two persistence responsibilities (platform index + provider record)
- Trade-off: Old provider outage may degrade historical detail to platform snapshot
- Trade-off: F2.1 requires ADO code changes beyond F2.0 scaffold

## Clarification — discovery/listing vs. detail authority (F2.2, 2026-09-01)

This ADR fixed identity, routing, and audit-snapshot ownership for the platform
canonical index. It did not originally say whether that index could power a list
endpoint. F2.2 answers this narrowly, without reopening the Model C decision above:

**The canonical index may serve discovery/listing from its creation-time snapshot;
the provider remains the detail/operational record authority.**

Concretely:

| Surface | Source | Authority |
|---|---|---|
| **List** (`GET /changes`) | Platform canonical index (`change_index`), creation-time snapshot | Discovery only — never claims to be live provider/workflow state |
| **Detail** (`GET /changes/:changeId`) | `indexRecord.providerKey` → `IChangeManagementProvider.get()` | Provider-authoritative canonical `Change`, per Model C above |

This is not a new authority — it is the same "audit snapshot… degraded read when
provider unavailable" purpose the index already had (see "Platform canonical index
(minimum durable fields)" above), now also serving routine discovery instead of only
a fallback path. It does **not** change:

- who owns the operational record (still the provider, per Model C);
- the anti-pattern above (a list backed by the index is not a `ChangeRepository`
  replacing the provider — detail still routes through `IChangeManagementProvider`);
- catalog snapshot semantics (list rows carry the same creation-time `ownerRef` as
  detail, never a live re-resolution).

A provider outage therefore degrades **detail only**: `GET /changes` keeps working
(it never touches `IChangeManagementProvider`), while `GET /changes/:changeId` for
an affected record returns `503 PROVIDER_UNAVAILABLE`. This is expected, not a bug —
see ADR-006's "List read scope (F2.2)" for the read-authorization predicate, which is
identical between list and detail.

## Related documents

- Backend contract: [ADR-006](./ADR-006-change-management-backend-contract.md)
- Provider agnosticism: [ADR-003](./ADR-003-provider-agnostic-change-management.md)
- Architecture review handoff: [`implementation-progress.md`](../backstage/implementation-progress.md) §10
- ADO implementation: `platform-devops-developer-portal` commit `b2bed17`
