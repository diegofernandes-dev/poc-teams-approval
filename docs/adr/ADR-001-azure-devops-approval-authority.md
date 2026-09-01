# ADR-001 — Azure DevOps remains deployment approval authority

- Status: Accepted historical baseline — superseded by ADR-009
- Date: 2026-08-27

> **Current target:** [ADR-009](./ADR-009-change-authorization-model.md) assigns
> business-change authorization to the platform Change Management capability and
> makes Azure DevOps an optional execution-eligibility consumer/enforcer. ADR-009
> supersedes this ADR's approval-authority direction. The decision below remains
> historical POC evidence; no ADO integration exists for the accepted target yet.

## Context

The solution exposes production approval interactions through Microsoft Teams and may later expose change-management context through Backstage or an ITSM tool. This creates a risk of accidentally creating multiple approval authorities with inconsistent state and audit trails.

The existing POC has already proven Azure DevOps Environment approval events, proactive Teams delivery, approval re-read, and approval updates. It also exposed an identity flaw when a service PAT was used to apply a human decision: Azure DevOps correctly audited the PAT owner rather than the Teams clicker.

## Decision

Azure DevOps is the authoritative system for:

- production deployment approval state;
- protected-resource policy;
- approver authorization;
- pending/approved/rejected status;
- environment protection;
- deployment approval audit.

Teams, Backstage, SharePoint, Jira Service Management, ServiceNow, and the Approval Gateway must not become parallel approval authorities.

The gateway must re-read the current Azure DevOps approval before applying any decision. Card/action payloads are correlation hints only and are not trusted for authorization or current state.

Integration failures must fail closed: if Teams, Backstage, the gateway, or a change-management provider is unavailable, Azure DevOps approval remains pending.

## Approval model

For the production Environment, the intended governance order is:

```text
Pre-check Approval
  Manager / business owner

Dynamic checks
  Optional technical validations

Post-check Approval
  CAB / operational authorization

Exclusive Lock
  Sequential production execution
```

The generic dynamic `Approval` check is not required for the current two-decision process.

The two approvals represent different decisions:

- Manager: "May this change be promoted to production?"
- CAB: "May this approved change execute in this production window/slot?"

## Consequences

- Teams can be replaced as a UI without changing approval authority.
- Change-management tooling can be replaced without changing deployment approval semantics.
- A Power Automate Approval, SharePoint approval workflow, or custom gateway approval state must not duplicate the Azure DevOps decision.
- Human approval applied through an integration must execute with the real user's delegated Azure DevOps identity or fall back to the Azure DevOps UI.
