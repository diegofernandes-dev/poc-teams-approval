# ADR-002 — Backstage is the change-request onramp

- Status: Accepted
- Date: 2026-08-27

## Context

The platform needs a consistent developer entry point for creating a GMUD/change request before production promotion. The underlying change-management system may change over time.

Backstage already provides a developer-facing portal, Software Catalog context, and Software Templates/Scaffolder workflows that can collect parameters and execute backend actions.

## Decision

Backstage is the preferred developer onramp for creating a production change request.

For the MVP, use a Software Template / Scaffolder flow rather than building a full custom Backstage plugin first.

The template collects only business/change information that cannot be safely derived from catalog or pipeline metadata. Where possible, Backstage derives application, owner, system, repository, pipeline, and team context from the Software Catalog.

The template calls a platform-owned change-management API/action and receives a canonical `changeId`.

Conceptually:

```text
Backstage
  -> Create Production Change
  -> Change Management API
  -> provider.create(...)
  -> changeId
```

The resulting `changeId` is the stable correlation key passed into the Azure DevOps production-promotion flow.

The pipeline must not receive SharePoint-, Jira-, or ServiceNow-specific identifiers or copy the full GMUD document into pipeline variables.

## MVP user flow

```text
Developer opens Backstage
  -> selects application/component
  -> fills change summary, risk, rollback and requested window
  -> Backstage creates change
  -> receives changeId
  -> production promotion is started with changeId
  -> HML executes
  -> PRD approvals begin
```

Starting the pipeline from the same Backstage workflow is desirable but is not a prerequisite for proving the change contract. The critical contract is that PRD promotion carries a valid `changeId`.

## Future evolution

A dedicated Backstage Change Management plugin may later provide views such as My Changes, Pending CAB, Scheduled, Failed Deployments, and change timelines.

That plugin is not required for the MVP. The Scaffolder is the initial onramp, not necessarily the final UI.

## Consequences

- Developers get one stable entry point even if the ITSM backend changes.
- Backstage does not become the deployment approval authority.
- The Backstage template must depend on the canonical change-management contract, not on SharePoint APIs directly.
- A full plugin is deferred until the workflow proves useful enough to justify richer lifecycle UX.
