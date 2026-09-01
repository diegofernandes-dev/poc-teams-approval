# Architect review brief — F3 change authorization

> Status: **accepted at F3.0.1 architecture convergence**.
>
> Date: 2026-09-01
>
> F3.0 proposal: bridge commit `7c050a7`
>
> F3.0 handoff / F3.0.1 baseline: bridge commit `6398d28`
>
> Implementation baseline: ADO `platform-devops-developer-portal` commit `6e28611`
>
> Normative decision: [ADR-009 — Change authorization model](./adr/ADR-009-change-authorization-model.md)

## Purpose and outcome

F3.0.1 resolves the two architecture corrections and all nine product/governance decisions that blocked F3.1 planning. ADR-009 is Accepted.

The Change Management capability owns provider-neutral business authorization and governance evidence. Humans decide; the platform derives authorization, post-execution compliance, and point-in-time execution eligibility. Azure DevOps and other execution systems consume eligibility but do not own approval state.

This review authorizes **F3.1 implementation planning only**. It authorizes no application code, migration, route, Teams integration, CAB UI, pipeline enforcement, or real ITSM provider.

## Converged architecture

```text
Change lifecycle                 Authorization                 Governance
submitted                       PENDING                       NOT_APPLICABLE/PENDING
  |                             AUTHORIZED                    COMPLIANT
  | accepted execution start    REJECTED                      NON_COMPLIANT
  v
executing
  | accepted completion
  v
completed  (post-execution governance may still be PENDING)
```

Key corrections:

1. `authorized` is removed from Change lifecycle. `AUTHORIZED` exists only in derived `AuthorizationEvaluation`.
2. `GovernanceEvaluation` is formalized over mandatory post-execution requirements and SLA evidence. Retrospective rejection or SLA breach creates `NON_COMPLIANT`, never retroactive unauthorized execution.

Execution eligibility remains separate: an authorized Change outside its window is `DENY`, and an `ALLOW` check does not start execution.

## Accepted product and governance decisions

| # | Decision | Accepted F3 MVP baseline |
|---|---|---|
| 1 | Normal/emergency policy matrix | Normal low = one configured pre-approval; normal medium/high = primary + CAB pre-approval; emergency = distinct A/B pre-approvals + post-execution CAB retrospective |
| 2 | Policy and selector publication | Deterministic reviewed application config; Platform/DevOps authors/operates, governance approves, publication creates immutable policy and selector versions |
| 3 | Additional mandatory approvers | Dedicated backend-authorized capability; users or configured authorities; accepted only before/as part of submission; additive only |
| 4 | Rejection/resubmission | Same `changeId`, next monotonic AuthorizationRound, new immutable snapshot/policy/principal resolution; rejected round remains immutable |
| 5 | Cancellation | Before execution: requester, owner, governance/admin; after start: governance/admin only; blocks new execution but does not stop running work |
| 6 | CAB attestation | One collective authority decision recorded by one currently authorized actor with recorder, authority, time, notes, meeting ref when available, and authorization evidence |
| 7 | Emergency retrospective | Mandatory, non-blocking; SLA is snapshotted policy; rejection or SLA miss = `NON_COMPLIANT`; escalation automation deferred |
| 8 | Separation of duty | Emergency A/B must resolve to distinct human decision actors; overlap fails submission closed; no F3.1 exception mechanism |
| 9 | Authorization permissions | Participant read, audit read, decide, CAB record, add requirement, cancel, policy admin, and governance read-all are separate server-enforced capabilities |

## Stable invariants

- Policy-generated requirements cannot be removed, replaced, downgraded, waived, or made optional.
- Submitted rounds, requirements, principal snapshots, decisions, and audit evidence are immutable.
- The greatest valid server-issued round number is current; no mutable current flag is required.
- Policy/configuration changes affect new rounds only.
- `approved` and `rejected` are the only F3 decision values.
- Participant visibility and `responsibleRef` grant no decision authority.
- Post-execution CAB never blocks emergency execution or retroactively revokes valid authorization.
- `completed` means execution completed, not authorization/CAB completion.
- A runtime `ALLOW` does not constitute execution-start evidence.
- No external provider, UI, channel, or execution product is authorization authority.
- DevOps owns policy/control reliability and exceptions, not happy-path per-deployment approval.

## F3.1 planning gate

The smallest next slice is **Authorization Ledger Foundation**:

- rounds, requirements, selector/principal snapshots, append-only decisions, and audit;
- deterministic published policy/configuration and selector resolver;
- submission-time first-round generation, additive-requirement permission, emergency separation-of-duty validation, and new-round semantics;
- server-authoritative idempotent decision command boundary;
- pure authorization/governance evaluators;
- permission-filtered Change-detail representation and separated authorization permissions.

Explicitly out of F3.1: Teams, CAB Workbench, Azure DevOps enforcement, public execution-check transport, execution lifecycle integration, automatic SLA/escalation, real ITSM providers, generic DSL/BPM, break-glass, post-submission requirement mutation, reversal, abstention, expiry, and technical stop-execution.

## Remaining deferred implementation decisions

No remaining question blocks F3.1 planning. Planning must still define literal permission strings, database/transport shapes, configuration module layout, release mechanics, retention controls, and error codes. Later slices own channel UX/authentication, target correlation, enforcement, provider projections, escalation automation, quorum/voting, and break-glass.

The exact retrospective SLA duration is deliberately published policy configuration, not an unresolved architecture decision.

## Review record

```text
Decision:     ACCEPT
Date:         2026-09-01
Decided by:   F3.0.1 architecture convergence checkpoint
Notes:        Two corrections and nine product/governance decisions incorporated in ADR-009.
```

## Gate

**GO for F3.1 implementation planning.**

**NO-GO for F3.1 implementation** until a reviewed implementation plan explicitly authorizes the slice. Wait for architecture review handoff; do not implement from this checkpoint.
