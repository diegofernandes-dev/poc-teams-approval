# ADR-009 — Change authorization model

- Status: Accepted — F3.0.1 architecture convergence
- Date: 2026-09-01
- Related: [ADR-002](./ADR-002-backstage-change-onramp.md), [ADR-003](./ADR-003-provider-agnostic-change-management.md), [ADR-006](./ADR-006-change-management-backend-contract.md), [ADR-007](./ADR-007-change-record-authority.md), [ADR-008](./ADR-008-multi-activity-change-execution-plan.md)
- Review packet: [Architect review brief — F3 change authorization](../architect-review-f3-change-authorization.md)

## Context

The current operational flow asks Platform/DevOps to approve a production pipeline after the organizational change process has already authorized the change. That second click is a bottleneck, not an independent governance decision.

F2 established a provider-neutral `Change`, Model C (platform canonical index plus provider operational record), durable identity/idempotency, multi-activity execution plans, and participant read policy. F3 adds an authorization architecture without making Azure DevOps, Teams, the Backstage UI, or an ITSM provider a second or sole approval authority.

The target principle is:

> Humans authorize the business change. The platform proves whether an execution is allowed.

Authorization, execution eligibility, execution lifecycle, and post-execution governance are related but orthogonal. A change may be authorized while its execution window is closed, and an execution may be completed while retrospective governance remains pending.

This ADR is architecture only. It introduces no route, schema, provider, Teams card, CAB screen, pipeline check, or application code.

## Decision

The Change Management capability owns a provider-neutral authorization ledger. For every submitted authorization round, an immutable published policy evaluates an immutable Change snapshot, produces effective requirements, resolves the required principals, and accepts append-only decisions.

The architecture exposes four distinct concepts:

| Dimension | Values | Meaning |
|---|---|---|
| Change lifecycle | `submitted`, `executing`, `completed`, `rejected`, `cancelled` | Business/execution milestone derived from accepted lifecycle events |
| Authorization evaluation | `PENDING`, `AUTHORIZED`, `REJECTED` | Derived result over mandatory pre-execution requirements in the current round |
| Governance evaluation | `NOT_APPLICABLE`, `PENDING`, `COMPLIANT`, `NON_COMPLIANT` | Derived result over mandatory post-execution requirements and their SLA |
| Execution eligibility | `ALLOW`, `DENY` with reasons | Point-in-time runtime decision for a claimed execution |

`authorized` is not a Change lifecycle value. `AUTHORIZED` belongs only to `AuthorizationEvaluation` and is never an independently mutable Change status.

```text
Change submitted
  -> immutable policy version + selector configuration
  -> AuthorizationRound
       -> effective requirements
       -> principal snapshots
       -> append-only decisions
  -> AuthorizationEvaluation
  -> ExecutionEligibility at request time
  -> accepted execution-start evidence
  -> executing
  -> accepted execution-completion evidence
  -> completed
  -> GovernanceEvaluation may still be PENDING
```

## Facts, events, evaluations, and projections

### Canonical persisted facts

The platform persists the minimum immutable evidence required to reproduce authorization, governance, and execution-eligibility outcomes:

- the Change identity and immutable snapshot evaluated by each round;
- round identity and monotonic round number;
- policy key/version, matched-rule provenance, and input fingerprint;
- immutable effective requirements and their source;
- selector key/version, resolved principal or authority ref, resolution time, and provenance;
- snapshotted post-execution SLA rule/version and anchor semantics;
- accepted decisions and actor-authorization evidence;
- append-only lifecycle, authorization, governance, and eligibility audit events.

All critical timestamps are server-controlled UTC.

### Append-only events

The audit stream records Change submission, round creation, policy selection, selector resolution, additional requirements, decisions, rejection, resubmission, cancellation, authorization reached, execution-eligibility checks, accepted execution start, accepted execution completion, post-execution decisions, and compliance/non-compliance milestones.

Events and accepted decisions are never destructively updated. Idempotent replay of the same command may return its original result; a conflicting command is rejected.

### Derived evaluations

`AuthorizationEvaluation`, `GovernanceEvaluation`, `ExecutionEligibility`, and current-round selection are deterministic derivations. They are not independently editable facts.

### Materialized projections

Implementations may materialize the current lifecycle and evaluations for query performance. A projection must be rebuildable from canonical facts and events and cannot become a competing authority. Provider, Backstage, Teams, and Azure DevOps copies are projections only.

## Change lifecycle

The lifecycle contains only:

```text
submitted --accepted execution start--> executing
executing --accepted completion-------> completed
submitted --mandatory pre rejection---> rejected
submitted/executing --accepted cancellation--> cancelled
rejected --accepted resubmission------> submitted  (same changeId, new round)
```

- `submitted` means a current submission exists; it says nothing about approval progress.
- `executing` begins only when a governed executor submits an accepted execution-start/evidence event. Merely checking and receiving `ALLOW` does not start execution.
- `completed` means actual execution completed. It never means authorization completed or CAB approved.
- `rejected` records rejection of the submitted round. It is terminal for that round, but the Change may be resubmitted under the same `changeId` with a new round.
- `cancelled` prevents new execution eligibility. It does not erase prior authorization or terminate an already-running technical process.

No `waiting_window`, `waiting_manager`, `waiting_cab`, or `cab_approved` lifecycle values are introduced. An implementation may preserve later technical completion evidence after a cancellation without pretending that cancellation stopped the process.

## Authorization policy and publication

`AuthorizationPolicy` is deterministic reviewed application code/configuration, not a generic rules engine, DSL, BPM engine, or workflow engine.

Platform/DevOps authors and operates the policy/configuration. The designated governance authority reviews and approves publication. Publication creates an immutable version:

```text
draft/config change
  -> governance review
  -> publish immutable policy and selector versions
  -> new rounds use the new published version
```

A policy has a stable key such as `default-change-authorization` and an immutable version such as `2026-09-01.1`. Published versions are never edited or reused for materially different content. Existing rounds remain bound to their recorded versions; a mutable “current policy” is never consulted to reinterpret history.

## Selectors and principal snapshots

Selectors are configuration identities such as `normal-primary-approver`, `emergency-approver-a`, `emergency-approver-b`, and `cab-authority`. Corporate titles, current employee names, and e-mail addresses are not canonical semantics.

At submission, the resolver snapshots:

- selector key and immutable version;
- resolved platform user or authority/group ref;
- server-controlled `resolvedAt`;
- resolver/configuration provenance.

For an individual requirement, the snapshot names a platform user. For a group/authority requirement, it retains the group/authority ref rather than expanding it to N required individuals. At decision time the authenticated actor must currently be authorized to act for that authority, and the decision snapshots the actual actor, authority, and authorization evidence.

If a selector cannot resolve, resolves to an ineligible principal, or violates separation of duty, submission fails closed. Later configuration changes do not alter existing rounds.

## Approval requirements and decisions

An effective `ApprovalRequirement` records stable round-local identity, kind (`individual`, `authority`, or `cab`), phase (`pre_execution` or `post_execution`), mandatory status, source/provenance, selector/principal snapshot, creation time, and applicable constraints.

An `ApprovalDecision` is an append-only `approved` or `rejected` fact for exactly one requirement and round. It records actor, authority where applicable, server UTC time, comment/reason, channel/correlation context, authorization evidence, and CAB meeting/reference where applicable. Rejection requires a reason.

A requirement accepts one terminal decision per round. Exact replay is idempotent; a conflicting second decision is rejected. F3 MVP has no reversal, abstention, decision expiry, or destructive repair.

## AuthorizationEvaluation

Authorization is derived over mandatory `pre_execution` requirements in the current round:

```text
any rejected mandatory pre requirement -> REJECTED
any undecided mandatory pre requirement -> PENDING
all mandatory pre requirements approved -> AUTHORIZED
```

Post-execution requirements do not participate. Cancellation also does not rewrite historical authorization; lifecycle and eligibility enforce cancellation separately.

`AUTHORIZED` means every mandatory pre-execution decision required by the snapshotted round is approved. It does not mean the window is active, the target matches, execution started, execution succeeded, or governance is complete.

## Authorization rounds and resubmission

Exactly one round is current for future execution eligibility:

- server-issued `roundNumber` values increase monotonically per `changeId`;
- a new round may be created only after the prior round is terminal;
- the current round is the valid round with the greatest `roundNumber`;
- no mutable `isCurrent` flag or destructive update is required.

A rejected requirement makes that round terminal `REJECTED`; later approval cannot repair it. Correcting and resubmitting the Change retains the same `changeId`, creates a new immutable Change snapshot, applies the currently published policy version, resolves new principal snapshots, and creates new requirements and decisions. All prior evidence remains immutable.

Example:

```text
CHG-42
  R1: P1 approved, CAB rejected -> REJECTED
  corrected and resubmitted
  R2: new snapshot, policy provenance, principals, requirements, decisions -> PENDING
```

A fundamentally different business change receives a new Change and `changeId`.

## Normal-change policy baseline

The F3 MVP policy baseline is configuration, not hardcoded lifecycle behavior:

| Classification/risk | Mandatory pre-execution requirements |
|---|---|
| Normal + low | One configured primary approval |
| Normal + medium | One configured primary approval + CAB |
| Normal + high | One configured primary approval + CAB |

For normal low risk, approval of the primary requirement yields `AUTHORIZED`; a closed window still yields execution `DENY`, and an open valid window/context may yield `ALLOW`.

For normal medium/high, one approval with CAB pending remains `PENDING`; both approved yields `AUTHORIZED`.

## Emergency policy baseline

Emergency requires:

- pre-execution Approver A;
- pre-execution Approver B;
- mandatory post-execution CAB retrospective.

A and B are generic configured selectors and must produce distinct human decision actors in the same round. Resolution to the same effective person fails submission closed. F3.1 has no exception mechanism.

When A and B approve, authorization is `AUTHORIZED`. The retrospective does not block emergency execution. After accepted start and completion, governance remains `PENDING` until the retrospective is approved, rejected, or misses its SLA.

## Additional mandatory requirements

Additional requirements may be proposed before or as part of submission only by an actor authorized through a dedicated backend-enforced capability such as `change.authorization.requirement.add`.

They may resolve to platform users or configured authorities/groups and are additive only. They cannot remove, replace, downgrade, waive, or make optional a policy-generated requirement. Once the Change is submitted, the active round and its effective requirements are immutable. An approver cannot dynamically add another approver in F3 MVP.

Example: policy produces `P1` and `P2`, and an authorized submission adds `A1`; all three must approve. `A1` cannot replace either policy requirement.

## Cancellation

Before execution begins, requester, Change owner, or governance/admin authority may cancel, subject to server-side authorization. After execution begins, requester alone cannot cancel; governance/admin authority is required.

Cancellation is append-only, blocks all new execution eligibility, and preserves prior decisions and evidence. It means “cancel future execution,” not “stop an already-running technical deployment.” A technical stop mechanism is deferred.

## CAB semantics

CAB is one collective governance authority, represented by a configured authority principal such as `group:default/cab-authority`. One authenticated actor currently authorized to act for that authority records the collective `approved` or `rejected` decision.

The decision captures the outcome, actual recorder, authority, server time, notes, meeting/reference when available, and evidence that the recorder could act for the authority. CAB does not require one click per participant by default; quorum, attendance, voting, or individual signatures are supporting evidence or a later specialized policy.

## GovernanceEvaluation

Governance is derived only from mandatory `post_execution` requirements:

```text
no mandatory post requirements                         -> NOT_APPLICABLE
any mandatory post requirement undecided within SLA    -> PENDING
all mandatory post requirements approved               -> COMPLIANT
any mandatory post requirement rejected                -> NON_COMPLIANT
any mandatory post requirement past its SLA undecided  -> NON_COMPLIANT
```

At round creation, the requirement snapshots the SLA policy key/version, duration or rule, and anchor semantics. For the emergency retrospective, the anchor is the server-controlled execution-completion event; the deadline is deterministically derived from that timestamp. The exact duration remains published configuration, not a domain constant.

A rejected or overdue retrospective creates audit evidence, a governance exception, and a future follow-up/escalation obligation. It never retroactively changes a valid historical execution to unauthorized. Escalation automation is deferred.

## Execution eligibility and execution evidence

`AUTHORIZED` is not “executable now.” A runtime eligibility check uses server time and requires:

```text
Change exists
AND current AuthorizationEvaluation = AUTHORIZED
AND lifecycle permits a new execution
AND no cancellation/security/policy hold applies
AND server time is inside [startsAtUtc, endsAtUtc)
AND target/context correlates to the governed Change
```

The result is `ALLOW` or `DENY` with stable reasons, evaluation time, and authorization evidence reference. It is audited but is never persisted as Change lifecycle. `ALLOW` does not mean execution started; a separate accepted execution-start/evidence boundary is required. Final transport, authentication, replay protection, target correlation, and reason-code vocabulary are deferred.

## Permission boundaries

Participant visibility remains requester, `ownerRef` team, any activity `responsibleRef` team, or `platform_admin`. That is read participation only.

The backend separately authorizes these capabilities:

| Capability | Meaning |
|---|---|
| Participant read | Read a Change due to participation |
| Authorization audit read | Read requirements, decisions, and actor evidence |
| Individual decision | Decide a requirement resolved to the actor |
| CAB record | Attest a collective CAB decision for an authority |
| Add requirement | Add an allowed mandatory requirement at/before submission |
| Cancel Change | Cancel under the lifecycle rules above |
| Policy administration | Author/review/publish policy and selector configuration |
| Governance read-all | Enterprise/auditor visibility across Changes |

Exact literal permission strings may be refined during F3.1 planning. The conceptual separation is mandatory. Participant read, `ownerRef`, and `responsibleRef` never automatically imply decision authority; the frontend never decides authorization itself.

## Model C authority implications

Model C remains a hybrid, not a platform-owned monolithic Change repository:

| Data | Canonical authority | Placement |
|---|---|---|
| Change identity, routing, creation snapshot | Platform | Canonical index |
| Policy and selector definitions/versions | Platform Change Management | Reviewed immutable application configuration |
| Authorization rounds and provenance | Platform Change Management | Authorization ledger linked by `changeId` |
| Requirements and principal snapshots | Platform Change Management | Authorization ledger |
| Decisions and actor evidence | Platform Change Management | Append-only authorization ledger |
| Accepted lifecycle milestones needed for eligibility | Platform Change Management | Append-only evidence/audit ledger |
| Authorization and governance evaluations | Platform Change Management | Derived/rebuildable projections |
| Execution-eligibility evaluations | Platform Change Management | Runtime result plus audit |
| Full operational GMUD detail, attachments, provider workflow | Configured ITSM provider | Provider record behind `IChangeManagementProvider` |
| ITSM/ADO/Teams/Backstage copies | Projection or interaction context | Never canonical authorization state |

Provider detail and platform authorization reads are composed. The ledger remains adjacent to the canonical index and does not replace `IChangeManagementProvider`. Azure DevOps is an optional future execution consumer/enforcer; its approval objects are not canonical. Teams and Backstage are interaction surfaces, never authorities.

This ADR supersedes ADR-001's target statement that Azure DevOps is the approval authority and ADR-007's former approval-authority row. The historical ADO-centric decisions remain documented as POC history.

## Audit requirements

An auditor must be able to reconstruct, in order: submission; policy version and input; round; requirements; selector resolution; additional requirements; every decision; rejection; resubmission; cancellation; authorization reached; eligibility checks; execution start; execution completion; post-execution governance; CAB retrospective; and compliance/non-compliance.

Audit uses stable principal refs and server-controlled UTC timestamps. Provider/channel identifiers are context only. Sensitive comments/evidence require retention and access controls to be defined during implementation planning.

## F3.1 planning boundary — Authorization Ledger Foundation

F3.1 implementation planning may include:

- authorization rounds, requirements, selector/principal snapshots, append-only decisions, and audit persistence;
- deterministic published policy/configuration and selector-resolution boundaries;
- submission-time first-round generation, authorized additive requirements, emergency separation-of-duty validation, and new-round domain semantics;
- a server-authoritative decision command boundary with idempotent replay and conflicting-decision rejection;
- pure AuthorizationEvaluation and GovernanceEvaluation functions;
- permission-filtered authorization/governance representation composed into Change detail;
- permission definitions/enforcement for audit read, decide, CAB record, add requirement, policy administration, governance read-all, and cancellation.

F3.1 explicitly excludes Teams, CAB Workbench, Azure DevOps enforcement, public execution-check transport, execution lifecycle integration, automatic SLA/escalation jobs, real ITSM providers, generic DSL/BPM, break-glass, post-submission requirement mutation, decision reversal, abstention, expiry, and technical stop-execution behavior.

The governance evaluator may be implemented and tested against supplied canonical timestamps without adding an execution integration or background timer.

## Rejected alternatives

| Alternative | Decision and rationale |
|---|---|
| DevOps final happy-path approval | Rejected — duplicates an already-made governance decision |
| Teams as relocated DevOps button | Rejected — changes the channel, not the authority or bottleneck |
| Hardcoded manager → CAB → DevOps workflow | Rejected — policy varies and DevOps is not a regular approver |
| Corporate-role-specific domain fields | Rejected — selectors resolve to provider-neutral principals |
| N CAB clicks by default | Rejected — CAB is one collective authority decision |
| Azure DevOps as authorization authority | Rejected — binds business authorization to one execution product |
| ITSM provider as sole authorization authority | Rejected — undermines platform enforcement and portability |
| Workflow-specific status explosion | Rejected — lifecycle, authorization, governance, and eligibility are orthogonal |
| Generic BPM engine | Rejected — the bounded model does not require arbitrary workflow orchestration |
| Policy DSL on day one | Rejected — deterministic reviewed configuration is smaller and testable |

## Deferred implementation decisions

No remaining decision blocks F3.1 planning. Planning must still choose literal permission names, persistence and transport shapes, configuration file/module layout, operational policy release mechanics, retention controls, and detailed error codes. Later slices own Teams authentication/cards, CAB Workbench UX, execution enforcement, target correlation, SLA escalation automation, ITSM projections, quorum/voting, advanced decisions, break-glass, and activity lifecycle.

The exact retrospective SLA duration is intentionally a published policy value, not an open domain decision.

## Required architecture review answers

| # | Question | Answer |
|---|---|---|
| 1 | Does Change.lifecycle still contain `authorized`? | No. It contains `submitted`, `executing`, `completed`, `rejected`, and `cancelled`. |
| 2 | Where does AUTHORIZED now live? | Only in derived `AuthorizationEvaluation`. |
| 3 | Can authorization and lifecycle diverge legitimately? | Yes; for example lifecycle `submitted`, authorization `AUTHORIZED`, window closed. |
| 4 | What does `completed` mean? | Actual execution completion evidenced by an accepted completion event. |
| 5 | Can completed have pending post-execution governance? | Yes. |
| 6 | What is GovernanceEvaluation? | A deterministic derivation over mandatory post-execution requirements and their SLA. |
| 7 | What states does GovernanceEvaluation have? | `NOT_APPLICABLE`, `PENDING`, `COMPLIANT`, `NON_COMPLIANT`. |
| 8 | Does retrospective CAB block emergency execution? | No. |
| 9 | What happens if retrospective CAB rejects? | Governance becomes `NON_COMPLIANT`; prior valid execution remains authorized-at-time-of-execution. |
| 10 | What happens if its SLA is missed? | Governance becomes `NON_COMPLIANT`; exception/follow-up evidence is recorded. |
| 11 | Are emergency Approver A/B required to be distinct? | Yes, as distinct human decision actors in the same round; overlap fails closed. |
| 12 | Are corporate titles present in canonical semantics? | No. |
| 13 | How are normal low-risk changes authorized? | One configured mandatory pre-execution approval. |
| 14 | How are normal medium/high changes authorized? | One configured mandatory pre-execution approval plus CAB pre-execution approval. |
| 15 | Can additional mandatory approvers weaken policy? | No; they are additive only. |
| 16 | Who may add them? | Only an actor granted the dedicated server-enforced capability. |
| 17 | Can requirements change after submission? | No; the active round is immutable. |
| 18 | What happens after rejection? | The round is terminal; correction/resubmission creates a new round and preserves history. |
| 19 | Same changeId or successor Change? | Same `changeId`; a fundamentally different business change gets a new Change. |
| 20 | Who may cancel before execution? | Requester, Change owner, or governance/admin authority, server-authorized. |
| 21 | What changes after execution begins? | Requester alone cannot cancel; governance/admin is required, and cancellation cannot stop the running process. |
| 22 | Who records CAB decisions? | One authenticated actor currently authorized to act for the snapshotted CAB authority. |
| 23 | Is CAB one collective decision? | Yes, by default. |
| 24 | Are participant-read and approval permissions separate? | Yes. |
| 25 | What owns authorization evidence under Model C? | Platform Change Management's authorization ledger. |
| 26 | Is ADO canonical approval authority? | No. |
| 27 | Is Teams canonical? | No. |
| 28 | Is Backstage UI canonical? | No. |
| 29 | Is ADR-009 now Accepted? | Yes. |
| 30 | Is architecture ready for F3.1 planning? | Yes — GO for implementation planning. |
| 31 | What belongs in F3.1? | The Authorization Ledger Foundation listed above. |
| 32 | What remains out of F3.1? | Channels/UI, enforcement, execution integration, escalation automation, real providers, generic workflow/policy engines, break-glass, and advanced decision semantics. |

## Gate

ADR-009 is **Accepted**. The architecture is **GO for F3.1 implementation planning**.

This does not authorize F3.1 implementation. Do not implement production code, migrations, routes, Teams, CAB UI, Azure DevOps enforcement, or a real ITSM provider from this checkpoint.
