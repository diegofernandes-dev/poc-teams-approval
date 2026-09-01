# ADR-009 — Change authorization model

- Status: Proposed — F3.0 architecture review required before F3.1 planning
- Date: 2026-09-01
- Related: [ADR-002](./ADR-002-backstage-change-onramp.md), [ADR-003](./ADR-003-provider-agnostic-change-management.md), [ADR-006](./ADR-006-change-management-backend-contract.md), [ADR-007](./ADR-007-change-record-authority.md), [ADR-008](./ADR-008-multi-activity-change-execution-plan.md)
- Review packet: [Architect review brief — F3 change authorization](../architect-review-f3-change-authorization.md)

## Context

The current operational flow asks Platform/DevOps to approve a production pipeline after the organizational change process has already authorized the change. That second human click is a bottleneck, not an independent governance decision.

F2 established a provider-neutral `Change`, Model C (platform canonical index plus provider operational record), durable identity/idempotency, multi-activity execution plans, and participant read policy. F3 must add authorization without redesigning those foundations and without making Azure DevOps, Teams, Backstage UI, or an ITSM provider a second or sole approval authority.

The target principle is:

> Humans authorize the business change. The platform enforces whether an execution is allowed.

Authorization and execution eligibility are related but different facts. A fully approved change is authorized even outside its execution window; it is executable only when all runtime constraints also pass.

This ADR is architecture only. It introduces no route, schema, provider, Teams card, CAB screen, pipeline check, or application code.

## Decision

The Change Management capability will own a provider-neutral **authorization instance** for each submitted authorization round. A versioned policy evaluates an immutable change snapshot, produces effective approval requirements, resolves and snapshots the required principals, and accepts append-only human/governance decisions.

The platform derives two separate results:

1. **Authorization evaluation** — whether every mandatory pre-execution requirement in the current round has a valid approval and none has been rejected.
2. **Execution eligibility** — whether an already authorized change may execute now, for the target and context presented by an execution system.

Azure DevOps and other execution systems will eventually consume execution eligibility. They do not own the approval state. Teams and Backstage are interaction surfaces. They do not own decisions.

```text
submitted Change snapshot
        |
        v
versioned AuthorizationPolicy
        |
        v
AuthorizationInstance / round
  +-- effective ApprovalRequirements
  +-- resolved principal snapshots
  +-- append-only ApprovalDecisions
        |
        v
AuthorizationEvaluation
        |
        +-- authorized business fact
        |
        v
ExecutionEligibility(changeId, targetRef, now, context)
        |
        +-- ALLOW
        +-- DENY with stable reason(s)
```

## Domain model

The names below describe semantics and boundaries. They do not freeze a TypeScript or persistence shape.

### AuthorizationPolicy

An `AuthorizationPolicy` is an immutable, versioned platform definition that derives authorization requirements from a submitted Change snapshot.

It has, conceptually:

- a stable policy key and immutable version;
- applicability criteria over provider-neutral Change characteristics such as classification, risk, primary/activity targets, system characteristics, and approved governance metadata;
- rules that produce requirement kinds, phases, selectors, and any separation-of-duty constraint;
- provenance sufficient to explain which rules produced each requirement.

F3 starts with deterministic application policy expressed as reviewed code/configuration. A generic rules DSL is not justified. Every material policy change creates a new immutable version; it never rewrites a version already used by a submitted change.

### AuthorizationInstance and authorization round

An `AuthorizationInstance` binds one Change to one historically stable authorization round. It contains or references:

- `changeId` and an authorization-round identity;
- the immutable policy key/version and policy input snapshot/fingerprint;
- effective requirements and resolved-principal evidence;
- append-only decisions;
- derived evaluation results and milestone audit records.

Requirements are not edited or replaced inside a round. If amendment/resubmission is later supported, it creates a new round, regenerates requirements, and preserves the prior round. Whether that round remains under the same `changeId` or creates a successor Change is a product decision required before F3.1.

### Effective ApprovalRequirement

An effective `ApprovalRequirement` states **what authorization is required**. It is not a decision and not a workflow task.

Each requirement has stable identity within its round and captures these semantics:

| Concern | Semantics |
|---|---|
| Kind | `individual`, `authority`, or `cab` for F3; the kind defines who may record the decision, not a UI |
| Phase | `pre_execution` or `post_execution` |
| Mandatory | Effective requirements in F3 are mandatory; an optional reviewer is a notification, not an approval requirement |
| Source | `policy` with rule provenance, or `additional` with actor/time/reason provenance |
| Selector snapshot | The configured selector identity plus its resolved platform principal snapshot |
| Creation | Server timestamp and authorization round |
| Constraints | Only narrowly required constraints, such as distinct decision actors for two emergency requirements |

“Optional additional mandatory approver” means adding the requirement is optional before submission; once added, satisfying it is mandatory. A mandatory post-execution requirement is mandatory for governance completion but does not block emergency execution.

F3 does not generalize phases into arbitrary workflow nodes. New phases require a later architecture decision.

### ApproverSelector and ResolvedPrincipalSnapshot

An `ApproverSelector` is versioned policy/configuration intent, not a corporate title or person embedded in the Change domain. Examples are a stable selector key resolving to a user, a Catalog group, or a governance authority.

At submission:

1. policy selects configured selectors;
2. a provider-neutral principal resolver resolves them to platform entity references;
3. the authorization instance snapshots the selector key/version, resolved principal ref(s), resolution time, and resolution provenance;
4. decisions later target the snapshotted requirement, not the current selector configuration.

For an individual requirement, resolution identifies a user principal such as `user:default/alice`. For an authority or CAB requirement, resolution identifies the authority principal such as `group:default/change-authority`; it does not expand the group into N individual requirements.

Group membership may change after submission. The required authority remains the snapshotted group ref. At decision time the platform verifies that the actor is then authorized to act for that authority and snapshots the actor's relevant authorization evidence with the decision. If a snapshotted principal is deleted or can no longer act, the requirement does not silently resolve to a replacement; governance must use an explicit exception/resubmission path.

Names, e-mail addresses, current employees, and corporate job titles are never canonical authorization semantics. Display labels may be resolved for UI only.

### ApprovalDecision

An `ApprovalDecision` is an immutable human/governance fact attached to exactly one requirement in one authorization round.

It records:

- `approved` or `rejected`;
- the stable requirement and round identities;
- the platform principal who made or recorded the decision;
- server-controlled decision time;
- comment/reason (required for rejection);
- decision-channel and request correlation as audit context, without making the channel authoritative;
- the evidence used to confirm the actor was eligible for the snapshotted principal/authority;
- CAB meeting/reference metadata when applicable.

F3 has only `approved` and `rejected`. `abstained` is equivalent to no decision for authorization. `cancelled` is a Change lifecycle action, not a decision. Approval expiry is deferred; the execution window expiring changes eligibility, not historical approval.

Accepted decisions are append-only and terminal for the requirement in that round. They are never overwritten. A correction requires a new authorization round or an explicit future reversal model; it must not update the old row. Replayed delivery of the same command may be idempotent, but cannot create conflicting accepted decisions.

### AuthorizationEvaluation

`AuthorizationEvaluation` is a deterministic derivation over the current round, not an independently editable status.

```text
cancelled Change                         -> CANCELLED
any rejected mandatory pre requirement -> REJECTED
any undecided mandatory pre requirement -> PENDING
all mandatory pre requirements approved -> AUTHORIZED
```

Only decisions made by an eligible actor against an active requirement in the current round are valid input. Post-execution requirements are deliberately excluded from the pre-execution authorization predicate.

The business meaning of **authorized** is:

> The submitted change, as snapshotted and evaluated under its recorded policy version, has every mandatory pre-execution governance decision required for that authorization round.

Authorization does not mean the window is active, the requested target matches, capacity is available, execution succeeded, or the change is complete.

A valid rejection of any mandatory pre-execution requirement makes the round rejected and moves the Change to terminal `rejected`. No later approval can repair that round. Resubmission, if offered, requires a fresh round and policy evaluation.

### ExecutionEligibility

`ExecutionEligibility` is a point-in-time, provider-neutral evaluation made for an execution request. It is not an approval and not a durable Change status.

Conceptually:

```text
executableNow =
  change exists
  AND current authorization round is AUTHORIZED
  AND Change lifecycle permits further execution
  AND server time is inside the approved execution window
  AND requested target/context correlates to the governed Change
  AND no cancellation or policy/security hold blocks execution
```

The requested execution window is treated as half-open `[startsAtUtc, endsAtUtc)`: executable at the start instant and no longer executable at the end instant.

The business meaning of **executable now** is:

> The platform can prove that this authorized change may perform this claimed execution against this governed target at the evaluation instant.

An eligibility result records the evaluation time, authorization round/evidence reference, outcome, and reasons. Example denial categories are change absent, not authorized, window inactive, cancelled/terminal lifecycle, or target mismatch. Exact public codes and transport are deferred to the enforcement contract design.

## Minimal lifecycle and orthogonal approval progress

The minimal target Change lifecycle is:

```text
submitted --all pre requirements approved--> authorized
authorized --first accepted execution------> executing
executing ----------------------------------> completed

submitted --mandatory pre rejection--------> rejected
submitted/authorized/executing --cancel----> cancelled
```

`submitted`, `authorized`, `executing`, `completed`, `rejected`, and `cancelled` are sufficient conceptual states. F3.1 must decide which execution transitions it actually implements; F3.0 adds none.

Approval progress remains orthogonal:

| Change lifecycle | Example authorization progress |
|---|---|
| `submitted` | 1 of 2 pre-execution requirements approved |
| `authorized` | all mandatory pre-execution requirements approved; window may still be closed |
| `executing` | authorization remains historically true; one or more activities are underway |
| `completed` | execution ended; a mandatory post-execution CAB review may still be pending |
| `rejected` | current round has a rejected mandatory pre-execution requirement |
| `cancelled` | no new execution is allowed; prior decisions remain auditable |

There are no statuses such as `waiting_manager`, `waiting_cab`, `cab_approved`, or `waiting_window`. Execution activity status remains outside this ADR and ADR-008 is not redesigned.

Cancellation is append-only and never deletes decisions. It blocks new eligibility immediately but cannot undo an execution already in flight. Detailed cancellation permissions and whether cancellation is allowed after execution begins must be decided before F3.1.

## Illustrative flows, not hardcoded policy

### Normal, low risk

```text
normal + low-risk Change submitted
  -> policy normal-low/vN
  -> one mandatory pre-execution individual/authority requirement
  -> eligible actor approves
  -> AUTHORIZED
  -> window inactive: DENY / window not active
  -> window active + target matches: ALLOW
```

### Normal, CAB-required

```text
normal Change submitted
  -> policy version determines CAB is required
  -> mandatory pre-execution individual/authority requirement
  -> mandatory pre-execution CAB requirement
  -> both approved: AUTHORIZED
  -> runtime window and target checks determine executableNow
```

Risk/classification mappings above are examples. The exact normal-change policy must be approved before F3.1 and must live in versioned policy, not hardcoded workflow statuses.

### Emergency

```text
emergency Change submitted
  -> mandatory pre-execution Approver A requirement
  -> mandatory pre-execution Approver B requirement
  -> mandatory post-execution CAB retrospective requirement
  -> A and B approve (distinct actors if policy requires)
  -> AUTHORIZED; retrospective CAB is still pending
  -> window + target checks pass: ALLOW
  -> execution completes
  -> CAB retrospective approved/rejected and audited afterward
```

The selectors named A and B are generic policy keys. Their resolved principal snapshots carry the actual platform principals. No title or current organizational hierarchy enters the domain.

A post-execution CAB decision never retroactively makes the emergency execution unauthorized. A rejection or missed retrospective creates a visible governance exception/non-compliance outcome and follow-up obligation; its SLA and escalation path must be decided before emergency implementation.

### Additional mandatory approver

```text
policy result
  +-- requirement P1 (policy, pre_execution, mandatory)
  +-- requirement P2 (policy, pre_execution, mandatory)

authorized additional selector supplied at/before submission
  +-- requirement A1 (additional, pre_execution, mandatory,
                       addedBy + addedAt + reason)

effective set = P1 + P2 + A1
AUTHORIZED only when P1, P2, and A1 are approved
```

Additional requirements are additive only. They cannot remove, replace, downgrade, waive, or make optional a policy-generated requirement.

Because the current product persists no draft and creates a Change as `submitted`, the F3.1-safe initial rule is:

- additional selectors may be proposed before/as part of submission by an actor with an explicit permission;
- the backend validates that permission and resolves them together with policy requirements;
- after submission, no requirement may be added, removed, or replaced in the active round;
- an approver cannot add another approver in F3.1;
- additional requirements may target individuals or authorities/groups when allowed by policy.

A future additive post-submission change would require a new round, explicit audit, and immediate loss of executable eligibility until the new effective set is approved. It must not mutate the current round silently.

## CAB decision semantics

CAB is one collective governance requirement, not N individual approval requirements by default.

A CAB requirement resolves to a snapshotted governance authority. One authenticated CAB operator/chair/delegate who is authorized to act for that authority records `approved` or `rejected`. Audit captures the collective outcome, recorder, decision time, notes, actor authorization evidence, and optional meeting reference.

This model does not assert that the recorder alone made the decision; it asserts that the authorized recorder attested the collective decision. If a regulator requires named attendance, quorum, votes, or individual signatures, those are supporting CAB evidence or a later specialized policy—not an automatic expansion of every CAB into N clicks.

## Channel and UX boundaries

### Teams

Teams is a future notification and individual-decision interaction adapter:

```text
ApprovalRequirement
  -> notification/interaction adapter
  -> Teams
  -> authenticated ApprovalDecision command
  -> Change Management capability
```

Teams does not store authoritative requirement or decision state. Card payloads are untrusted correlation data. The Change Management capability authenticates the actor, re-reads the requirement, authorizes the actor against the snapshotted principal, accepts the command, and records the decision. Teams may be a good channel for individual or exceptional approvals; this ADR does not design a card or delegated-auth implementation.

### Backstage CAB Workbench

The preferred CAB interface is a future Backstage governance workspace, because CAB decisions require queue and cross-change context rather than isolated mobile buttons.

Its future read/query model should assemble:

- CAB-pending and post-execution-review requirements;
- classification, risk, requested window, primary/activity targets, and affected system;
- execution plan, rollback, evidence, and provider-authoritative operational detail;
- related/overlapping changes and execution windows;
- effective requirements, decision history, and current authorization outcome.

The workbench may record a CAB decision through the same platform command boundary. Backstage UI itself is not the authority; the Change Management authorization ledger is. Teams may notify CAB members and deep-link to this workspace.

## Future execution-enforcement boundary

An execution system will present a provider-neutral request conceptually containing:

| Input | Meaning |
|---|---|
| `changeId` | Stable business-change identity |
| `targetRef` | Governed target the caller claims it will affect |
| `executionKind` | Optional provider-neutral category needed for matching policy |
| request context | Authenticated caller and correlation metadata; provider-specific details remain outside canonical Change |

The platform uses server time, not a caller-provided timestamp, for eligibility. A caller timestamp may be retained only as audit context.

The response conceptually contains `ALLOW` or `DENY`, evaluation time, stable reason(s), and an authorization-evidence reference/digest. Exact HTTP shape, authentication, replay protection, target matching rules, and reason codes are deferred until the enforcement slice.

No canonical Change field contains `pipelineId`, `buildId`, `environmentId`, `approvalId`, or an ITSM-specific key. Adapter telemetry may record such values as execution-request context without turning them into business authorization identity.

## Model C authority implications

ADR-007 Model C remains, with one bounded refinement: **the platform Change Management capability is authoritative for authorization evidence required to make platform execution decisions.** This does not make Backstage the enterprise ITSM database and does not make the platform authoritative for all operational GMUD detail.

| Data | Authority under F3 target | Placement |
|---|---|---|
| Policy definitions and versions | Platform Change Management | Versioned platform policy store/config |
| Authorization round identity/provenance | Platform Change Management | Canonical index linkage plus authorization ledger |
| Effective requirements | Platform Change Management | Platform authorization ledger keyed by `changeId` |
| Resolved principal snapshots | Platform Change Management | Platform authorization ledger |
| Approval decisions and actor evidence | Platform Change Management | Append-only platform authorization ledger |
| Authorization evaluation/milestones | Platform Change Management | Derived projection plus append-only audit |
| Execution eligibility evaluations | Platform Change Management | Runtime result plus audit record |
| Canonical change identity/routing/snapshot | Platform | Existing canonical index per ADR-007 |
| Full operational GMUD record, attachments, provider workflow detail | Configured ITSM provider in production | Provider record behind adapter |
| Provider copy of requirements/decisions | Optional projection only | Never authorization authority |

The authorization ledger is adjacent to and linked from the canonical index; it is not a monolithic replacement for `IChangeManagementProvider`. Future detail/CAB queries compose provider-authoritative operational content with platform-authoritative authorization content.

Provider replacement therefore preserves the authorization instance and audit history without translating provider approval objects. Historical operational detail still follows ADR-007's immutable `providerKey` routing/migration rules.

An external provider outage may limit operational detail, but it must not cause the platform to invent approval state. Whether an execution eligibility check can proceed from the complete platform snapshot during provider outage is a fail-closed policy decision to make in the enforcement slice.

Upon acceptance, this ADR supersedes ADR-001's statement that Azure DevOps is the canonical approval authority and ADR-007's corresponding authority-table row. Azure DevOps may continue to enforce technical protections during migration, but its Environment approval object is not canonical business authorization state.

## Audit model

F3 uses an append-only authorization/audit ledger plus current projections. It does not require event sourcing of the entire Change aggregate.

An auditor must be able to reconstruct, in order:

1. Change submission and immutable policy input snapshot/fingerprint;
2. policy key/version and matched rules;
3. generated requirements and their source;
4. configured selectors and resolved principal snapshots;
5. additional requirement actor, time, permission outcome, and reason;
6. every accepted decision, actor, server time, comment, channel, and actor-eligibility evidence;
7. rejection, cancellation, authorization-reached, and lifecycle milestone records;
8. later execution eligibility checks, request target/context, result, and reasons;
9. post-execution CAB outcome or unresolved governance obligation.

Records use UTC server timestamps and stable platform principal refs. Provider and channel identifiers are audit context only. Sensitive comments/evidence require retention and access controls; those controls are implementation planning concerns. Audit records are never destructively rewritten when policy, organization, provider, or display names change.

## Exception ownership

Platform/DevOps owns policy, control implementation, integration reliability, observability, and exceptions such as correlation failure, security exceptions, platform outage, invalid authorization evidence, and governed break-glass. It does not approve individual happy-path production changes.

Break-glass is not an implied bypass in this model. It requires a separately approved policy, strong audit, limited principals, reason/evidence, and retrospective review before implementation.

## Consequences

- Positive: the same business authorization can govern ADO pipelines and future execution systems without coupling the Change domain to either.
- Positive: submitted requirements remain historically stable across policy, organization, channel, and provider changes.
- Positive: individual approval UX and CAB governance UX can evolve independently while sharing one authority.
- Positive: post-execution emergency governance is supported without pretending it blocks prior execution.
- Positive: DevOps leaves the normal per-deployment approval chain and focuses on platform policy and exceptions.
- Trade-off: Model C gains a bounded platform-owned authorization ledger and composed reads.
- Trade-off: policy/principal resolution availability becomes part of submission reliability; failures must fail closed.
- Trade-off: provider projections can lag and must be labeled non-authoritative.
- Trade-off: cancellation, resubmission, and exception permissions require explicit product decisions before implementation.

## Rejected alternatives

| Alternative | Decision and rationale |
|---|---|
| DevOps remains final pipeline approver | Rejected — duplicates an already-made governance decision and preserves the bottleneck |
| Teams moves DevOps's Approve button to mobile | Rejected — changes the button location, not the authority or unnecessary human step |
| Hardcoded manager → CAB → DevOps workflow | Rejected — policy varies by classification/risk and DevOps is not a normal approver |
| Corporate-role-specific domain fields | Rejected — titles, names, and hierarchy change; versioned selectors resolve to platform principals |
| N individual CAB approvals | Rejected as default — CAB is one collective authority decision; quorum/voting is a specialized future requirement |
| External ITSM provider is sole authorization authority | Rejected — provider replacement/outage would undermine platform execution checks and historical portability |
| Azure DevOps Environment approval is canonical | Rejected — binds business authorization to one execution product/object |
| Dozens of workflow-specific Change statuses | Rejected — requirement/decision progress is orthogonal to the small lifecycle |
| Fully generic BPM/workflow engine | Rejected — pre/post authorization phases do not justify arbitrary process orchestration |
| Rules engine/DSL from day one | Rejected — deterministic versioned application policy is smaller, testable, and evolvable |

## Open decisions

### Must decide before F3.1 planning

1. Exact normal and emergency policy matrix by classification/risk/target, including when CAB is required.
2. Policy storage/version publication process and the governed selector catalog/resolver configuration.
3. Which principals/permissions may propose additional mandatory requirements at submission.
4. Rejection amendment/resubmission product semantics: a new round under the same `changeId` or a successor Change.
5. Cancellation permissions, allowed lifecycle points, and effect on already-started execution.
6. CAB recorder/delegate authorization and minimum collective-decision evidence.
7. Emergency retrospective CAB SLA, escalation, and the meaning of a rejected/missed retrospective.
8. Whether emergency Approver A/B must always be distinct and how overlapping resolved selectors fail validation.
9. Authorization read/decision permissions, including auditor and governance-wide visibility.

### Can defer

- Teams Adaptive Card design and delegated/channel authentication mechanics.
- CAB Workbench visual design, pagination, overlap algorithm, and meeting integration.
- Public pipeline-check transport, ADO adapter, execution caller authentication, replay protection, and final denial-code vocabulary.
- Detailed target-correlation rules and execution-kind vocabulary.
- SharePoint/Jira/ServiceNow projections and synchronization behavior.
- `abstained`, decision cancellation/reversal, or approval-expiry states.
- Quorum/vote/attendance modeling beyond the single CAB authority decision.
- Generic policy DSL, BPMN/workflow engine, and arbitrary phases.
- Break-glass implementation and activity-level execution lifecycle.

## Required architecture review answers

| # | Question | Answer |
|---|---|---|
| 1 | Business meaning of authorized | All mandatory pre-execution requirements in the snapshotted current round have valid approvals under the recorded policy version |
| 2 | Business meaning of executable now | The authorized change also passes lifecycle, server-time window, target-correlation, and hold checks for this request |
| 3 | Approval progress vs Change lifecycle | Separate; progress lives in requirements/decisions, with only meaningful lifecycle milestones on Change |
| 4 | What generates requirements | A deterministic, versioned platform policy evaluated at submission, plus authorized additive requirements |
| 5 | Can additional mandatory approvers be added | Yes, at/before submission in the initial design by explicitly authorized actors |
| 6 | Can they remove policy requirements | No; additions can never weaken policy output |
| 7 | Are job titles/names encoded | No; selectors resolve to provider-neutral principal refs |
| 8 | How approvers are snapshotted | Selector key/version, resolved principal ref(s), time, and provenance are frozen in the authorization instance |
| 9 | Organization config changes later | Existing rounds keep their snapshots; new submissions use the new configuration |
| 10 | Emergency vs normal | Policy may require multiple generic pre-execution approvers plus post-execution CAB; exact mappings remain configurable |
| 11 | Can post-execution CAB avoid blocking emergency execution | Yes; it gates governance completion/follow-up, not pre-execution authorization |
| 12 | CAB one decision or N clicks | One collective authority decision by default, recorded by an authorized operator/delegate |
| 13 | Is Teams system of record | No; it is a notification/interaction adapter |
| 14 | Is Backstage the future CAB workspace | Yes, preferred UI; the backend authorization ledger remains authoritative |
| 15 | Does DevOps remain in happy-path approvals | No; DevOps owns policy, controls, reliability, and exceptions |
| 16 | Does Azure DevOps own authorization | No; it is a future execution consumer/enforcer |
| 17 | Future pipeline contract | Provider-neutral `ExecutionRequest` (`changeId`, `targetRef`, optional execution kind/context) → `ALLOW`/`DENY` with reasons/evidence ref |
| 18 | Model C owner of requirements/decisions | Platform Change Management authorization ledger |
| 19 | Can authorization survive ITSM replacement | Yes; it is platform-owned and provider-neutral |
| 20 | Must decide before F3.1 | The nine product/governance decisions listed above |
| 21 | Can defer | Channels/UI, provider adapters, final enforcement transport, advanced decision/workflow semantics |
| 22 | Ready for F3.1 implementation planning | **NO-GO** until the must-decide list is resolved and this ADR is accepted |

## Gate

The F3.0 architecture is ready for stakeholder review. It is **NO-GO for F3.1 implementation planning** until the “Must decide” items are resolved and this ADR changes from Proposed to Accepted.

Do not implement F3.1, Teams, CAB UI, pipeline enforcement, or a real ITSM provider from this checkpoint.
