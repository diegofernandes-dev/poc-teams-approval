# Architect review brief — F3 change authorization

> Status: **awaiting architecture/stakeholder decision**.
>
> Date: 2026-09-01
>
> Architecture baseline: bridge commit `7c050a7`
>
> Implementation baseline: ADO `platform-devops-developer-portal` commit `6e28611`
>
> Normative proposal: [ADR-009 — Change authorization model](./adr/ADR-009-change-authorization-model.md)

## Purpose

Give the solution/governance architect a concise review packet for F3.0 without
requiring the ADO-centric Teams POC history to be interpreted as the target model.

This is a decision brief only. It authorizes no implementation, Teams integration,
CAB UI, pipeline enforcement, database change, or real ITSM provider.

## Decision requested

Review and either accept ADR-009 or return explicit changes. The primary decision is:

> The Change Management capability owns provider-neutral business-change
> authorization evidence. Humans decide; the platform derives authorization and
> execution eligibility. Azure DevOps and other execution systems consume that
> eligibility and do not own the canonical approval state.

If accepted, ADR-009 supersedes the target direction—not the historical POC
facts—of ADR-001, ADR-004, and ADR-005.

## Why the decision is needed

The present operational flow asks DevOps to click Approve after the organization
has already authorized the change. This adds throughput cost without an independent
governance decision. Moving the same click to Teams would preserve the bottleneck.

The proposed model removes DevOps from normal per-deployment approval while keeping
Platform/DevOps responsible for policy, controls, integration reliability,
observability, security exceptions, and break-glass governance.

## Proposed architecture in one view

```text
Change submitted
  -> versioned AuthorizationPolicy
  -> effective ApprovalRequirements
       = policy-generated + additive mandatory requirements
  -> resolved principal snapshots
  -> immutable ApprovalDecisions
  -> AUTHORIZED
  -> runtime window/lifecycle/target evaluation
  -> EXECUTABLE NOW
  -> execution consumer receives ALLOW or DENY
```

Key boundaries:

- approval progress is separate from the small Change lifecycle;
- authorization is separate from point-in-time execution eligibility;
- emergency policy may require two generic pre-execution approvers and a
  non-blocking post-execution CAB retrospective;
- CAB is one collective governance decision by default, recorded by an authorized
  operator/delegate—not N mandatory clicks;
- Teams is a future individual-decision channel, never a system of record;
- Backstage is the preferred future CAB Workbench UI, but the backend authorization
  ledger is authoritative;
- Model C remains: provider owns operational GMUD detail, while the platform owns a
  bounded authorization ledger needed for execution decisions;
- policy, requirements, principals, and decisions contain no job title, current
  employee name, e-mail address, ADO approval ID, or ITSM-specific ID.

## Decisions required before F3.1 planning

The architect/governance reviewers must resolve all rows below. ADR-009 intentionally
does not invent organizational policy.

| # | Decision | Required outcome |
|---|---|---|
| 1 | Normal/emergency policy matrix | Approved mapping from classification/risk/target characteristics to pre/post requirements and CAB |
| 2 | Policy and selector publication | Owner, review process, immutable version format, and selector resolver/config source |
| 3 | Additional mandatory approvers | Exactly who may propose them at submission and for which principal kinds |
| 4 | Rejection/resubmission | New round under the same `changeId` or successor Change; prior evidence must remain immutable |
| 5 | Cancellation | Authorized actors, allowed lifecycle points, and behavior after execution begins |
| 6 | CAB attestation | Who may record the collective decision and minimum notes/meeting evidence |
| 7 | Emergency retrospective | SLA, escalation, and meaning of rejected or missed post-execution CAB |
| 8 | Separation of duty | Whether emergency A/B must always be distinct and how selector overlap fails |
| 9 | Authorization permissions | Who may read full audit, decide, administer policy, and perform governance-wide/auditor queries |

## Recommended architecture invariants

These are not product-policy choices and should remain unless the architect records
a specific counter-decision:

1. Policy-generated requirements cannot be removed, downgraded, or bypassed.
2. Submitted authorization rounds and accepted decisions are append-only.
3. Policy/configuration changes affect new rounds only.
4. `approved` and `rejected` are the only F3 decision values.
5. Post-execution requirements do not retroactively revoke prior authorization.
6. Execution window is a runtime constraint, not a human approval.
7. No external provider or execution product is the sole authorization authority.
8. DevOps is not a regular happy-path approver.

## Explicitly deferred

- Teams card and authentication design;
- CAB Workbench visual design;
- public execution-check transport and ADO adapter;
- final target-correlation rules and denial-code vocabulary;
- ITSM projections;
- quorum/voting, abstention, approval expiry, arbitrary workflow phases, BPMN, or a
  generic policy DSL;
- implementation of break-glass or activity lifecycle.

## Review record

Record the outcome in ADR-009 and update its status only after the nine mandatory
decisions are resolved.

```text
Decision:     (ACCEPT / CHANGES REQUIRED)
Date:         …
Decided by:   …
Notes:        …
```

## Gate

**Current result: NO-GO for F3.1 implementation planning.**

F3.1 becomes eligible for planning only after the review record is completed,
ADR-009 is accepted, and the nine required decisions are reflected in the
normative architecture.
