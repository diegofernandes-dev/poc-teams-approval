# ADR-008 — Multi-activity change execution plan

- Status: Accepted (F2.1.2 architecture review)
- Date: 2026-09-01
- Related: [ADR-002](./ADR-002-backstage-change-onramp.md), [ADR-003](./ADR-003-provider-agnostic-change-management.md), [ADR-006](./ADR-006-change-management-backend-contract.md), [ADR-007](./ADR-007-change-record-authority.md)

## Context

F2.1.1 (ADO commit `ed6810b`) models a GMUD as a mostly atomic change: one `targetRef`, one catalog-derived `ownerRef`, one `requestedWindow`, one `rollbackPlan`. Real production changes often require **multiple execution activities performed by different teams** (DBA, Network, Platform, Application) under a single governed GMUD.

The platform must answer **"how will this change be executed and by whom?"** without becoming a workflow engine, Jira-like task system, or execution orchestrator.

## Decision

Introduce **`ExecutionPlan`** and **`ExecutionActivity`** as canonical, provider-neutral domain types on `Change`. Activities are **planned units of work with clear responsibility** — immutable creation-plan data, not executable workflow nodes.

### Canonical structure

```text
Change
├── ...existing fields...
└── executionPlan
        └── activities[]
                ├── activityId      (server-assigned at create)
                ├── title
                ├── description
                ├── responsibleRef  (Catalog Group)
                └── targetRef?      (optional activity-specific Component)
```

### Terminology

| Concept | API / domain | UI (PT) |
|---------|--------------|---------|
| GMUD governance owner | `Change.ownerRef` | Responsável |
| Activity performer team | `ExecutionActivity.responsibleRef` | Equipe executora |
| Planned work bundle | `ExecutionPlan` | Plano de execução |
| Single planned step | `ExecutionActivity` | Atividade |

Do **not** use generic `Task` naming — it implies workflow execution.

### `executionPlan` cardinality

**Required with `minItems: 1`.** Simple one-team changes submit exactly one activity. No implicit/default server-side activity.

### `Change.targetRef` semantics (retained)

- `Change.targetRef` (required): primary governed target and catalog anchor for `ownerRef` / `systemRef` enrichment (ADR-006).
- `ExecutionActivity.targetRef` (optional): activity-specific governed target when it differs from the primary target.
- Omitted activity `targetRef` means **no activity-specific catalog target** — not implicit inheritance in storage.

### `ownerRef` vs `responsibleRef`

| Field | Scope | Source |
|-------|-------|--------|
| `Change.ownerRef` | Whole GMUD | Server-derived from `Change.targetRef` |
| `ExecutionActivity.responsibleRef` | Single activity | Client-supplied |

Responsible team ≠ GMUD owner ≠ manager approver ≠ deployment approver.

### `responsibleRef` entity kind

**Catalog `Group` only** (`group:...`). Validated: parseable entity ref, kind = Group, entity exists in Catalog. User-level responsibility deferred to a future ADR.

### Activity `targetRef`

Optional. When present: valid catalog entity ref, kind `Component` (F2.1.2 scope). Firewall/process activities omit `targetRef`. Broadening to Resource/System requires a future ADR.

### Ordering

**Array order is authoritative.** No `sequence` field. No `dependsOn` / DAG. Platform does not enforce or execute ordering.

### Execution window

**`Change.requestedWindow` only.** No per-activity windows in F2.1.2.

### Rollback

**`Change.rollbackPlan` only.** No per-activity rollback in F2.1.2.

### Activity status — explicitly rejected

No per-activity lifecycle: `pending`, `in_progress`, `completed`, `failed`, `skipped`. `Change.status` remains `submitted` only.

### Activity approval — explicitly out of scope

No activity approver, approval state, CAB mapping, or ADO approval mapping. Approval architecture remains separate.

### Provider contract

`executionPlan` is part of the canonical provider-neutral `Change` model. `IChangeManagementProvider.create` / `get` receive and return the full `Change` including `executionPlan`. No provider-specific activity fields.

### Platform canonical index (ADR-007)

Persist **full immutable creation-time `executionPlan` snapshot** (with server-assigned `activityId`s) in the platform audit snapshot. This is creation-plan record data — not a workflow database.

### Authorization

**`responsibleRef` does NOT grant read access in F2.1.2.** GET scope unchanged: `platform_admin`, `requestedBy`, or `change.ownerRef` membership. Team-scope read for activity-responsible groups is an F3 policy question.

### Idempotency

`executionPlan` is part of `CreateChangeHttpRequest` and the payload hash. Same `Idempotency-Key` + different activities → `409 CONFLICT`. `activityId` is server-assigned and excluded from client hash input.

### HTTP input types

```typescript
type ExecutionActivityInput = {
  title: string;           // 3–200 chars, trimmed
  description: string;     // 10–2000 chars, trimmed
  responsibleRef: string;  // Catalog Group entity ref
  targetRef?: string;      // optional Component
};

type ExecutionPlanInput = {
  activities: ExecutionActivityInput[];  // min 1, max 20
};
```

### Persisted types

```typescript
type ExecutionActivity = ExecutionActivityInput & {
  activityId: string;  // UUID, server-assigned at create
};

type ExecutionPlan = {
  activities: ExecutionActivity[];
};
```

## Explicit non-goals (F2.1.2)

- Activity workflow/status tracking
- Activity approvals
- `dependsOn` / DAG dependencies
- Per-activity execution windows or rollback plans
- Pipeline, deployment, Jira, ServiceNow, or ADO linkage on activities
- Frontend wiring (deferred)
- ITSM provider implementation

## Consequences

- Positive: Cross-team GMUDs have structured accountability without a workflow engine.
- Positive: Simple deploys remain one activity — no ceremony.
- Positive: Additive to Model C; provider receives full business record.
- Trade-off: `executionPlan` required on POST — breaking for API clients without it.
- Trade-off: Catalog validation cost scales with activity count (capped at 20).

## Related documents

- Backend contract: [ADR-006](./ADR-006-change-management-backend-contract.md)
- Record authority: [ADR-007](./ADR-007-change-record-authority.md)
- UI contract: [`gmud-create-screen.md`](../ui/gmud-create-screen.md)
- ADO implementation: `platform-devops-developer-portal` branch `feat/ado-repo-governance`
