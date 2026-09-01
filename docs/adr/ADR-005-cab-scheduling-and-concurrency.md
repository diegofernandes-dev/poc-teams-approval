# ADR-005 — CAB scheduling uses deferred approval plus sequential locking

- Status: Proposed historical design — proposed supersession by ADR-009
- Date: 2026-08-27

> **F3.0 target:** [ADR-009](./ADR-009-change-authorization-model.md) models CAB as
> one provider-neutral governance decision and separates authorization from runtime
> window eligibility. If ADR-009 is accepted, CAB-as-an-ADO-post-check/deferred-
> approval is not the canonical model. Execution-system locking may remain a
> technical safety control, not business authorization state.

## Context

The current CAB process can release many production pipelines at the same time. That creates a burst of runnable deployment work and unnecessary pressure on Azure DevOps parallelism and downstream deployment capacity.

A pending Environment approval does not need to consume an agent. The concurrency problem occurs when many approvals become effective together.

Azure DevOps supports Pre-check Approval, dynamic checks, Post-check Approval, Deferred Approval, and Exclusive Lock. The documented check order is:

1. static checks;
2. pre-check approvals;
3. dynamic checks;
4. post-check approvals;
5. exclusive lock.

## Proposed decision

Use two separate human decisions:

```text
Pre-check Approval = manager/business owner
Post-check Approval = CAB
```

The CAB approval should carry an effective deployment slot, for example 22:00, 22:05, 22:10, rather than releasing all pending production deployments simultaneously.

Use Azure DevOps Deferred Approval where the platform provides a supported mechanism for that effective time.

Add Exclusive Lock with `lockBehavior: sequential` as a technical safety net so overlapping slots still execute sequentially against the protected production resource.

```text
CAB scheduling
  = controls when a deployment becomes eligible

Exclusive Lock sequential
  = protects against actual overlap
```

Business Hours alone is not the concurrency solution because multiple waiting runs can become eligible together when the window opens.

## Open technical question

The public Azure DevOps Approvals and Checks Update REST API currently documents approval status/comment/approver update semantics but does not document a Deferred Approval `effectiveAt` / `deferredUntil` field.

Therefore the MVP must not depend on an undocumented API for custom scheduling.

## MVP fallback

Until a supported programmatic deferred-time mechanism is proven:

- manager approval may be exposed through Teams once ADR-004 is accepted;
- CAB performs the Post-check Approval and Deferred Approval using the native Azure DevOps UI;
- Exclusive Lock remains enabled with sequential behavior;
- the custom UI may display the requested change window but must not pretend to schedule Azure DevOps if it cannot do so through a supported interface.

## Validation required

Before moving CAB scheduling into Teams/Backstage, prove one of:

1. a supported Azure DevOps API can set Deferred Approval effective time; or
2. a different supported Azure DevOps check model can implement the slot without creating a second approval authority.

Do not solve this by storing a human access token until the scheduled time, by applying the future decision with a shared service credential, or by creating an independent approval state in SharePoint/Power Automate.

## Consequences

- The MVP can ship a coherent governance flow without blocking on undocumented scheduling automation.
- CAB scheduling and Teams authentication remain separate technical problems.
- `sequential` lock is defense in depth, not a substitute for CAB scheduling policy.
