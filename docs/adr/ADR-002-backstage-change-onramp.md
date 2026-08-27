# ADR-002 — Backstage is the change-request onramp

- Status: **Accepted**
- Date: 2026-08-27

## Context

The platform needs a stable developer-facing entry point for production change requests without coupling that experience to SharePoint, Jira Service Management, ServiceNow, or another ITSM implementation.

Backstage is already the intended developer portal and can collect change-request data while deriving application metadata from the Software Catalog.

## Decision

Backstage is the developer onramp for creating production change requests.

For the MVP, use a Backstage Software Template / Scaffolder-based flow rather than building a full custom plugin first.

The user-facing capability is conceptually:

```text
Backstage
  -> Create Production Change
  -> Change Management API
  -> provider adapter
  -> changeId
```

The pipeline receives only the stable `changeId` correlation identifier. Provider-specific fields must not leak into pipeline contracts.

The initial Backstage form should derive whatever it reasonably can from catalog context, including application, owner/team, repository/pipeline links, and artifact metadata, and ask the user only for change-specific information such as summary, risk, deployment window, rollback plan, and supporting evidence.

## UI implementation contract

The MVP visual design is defined by:

- [`docs/ui/gmud-create-screen.md`](../ui/gmud-create-screen.md)
- [`docs/ui/gmud-create-reference.jpg`](../ui/gmud-create-reference.jpg)

The reference image is normative for the **overall composition, information hierarchy, section ordering, right-side approval summary, and primary actions**. Agents implementing the screen should preserve this layout unless an explicit ADR supersedes it.

Backstage's existing theme/components should still be used instead of hard-coding screenshot pixels. The goal is visual and structural fidelity while remaining a maintainable Backstage UI.

## MVP versus future evolution

### MVP

Use Scaffolder / form-driven creation with the documented layout and fields.

### Future

If change management becomes a first-class platform capability, evolve to a dedicated Backstage plugin exposing capabilities such as:

```text
Changes
- My Changes
- Pending CAB
- Scheduled
- Completed
- Create Change
```

The dedicated plugin must reuse the same Change Management API and canonical model rather than bypassing them.

## Consequences

### Positive

- Single developer experience regardless of ITSM backend.
- Existing catalog metadata can reduce manual form entry.
- Enables MVP delivery without prematurely building a large plugin.
- Allows a later custom plugin without changing the domain contract.

### Negative / risks

- Scaffolder UX may eventually become limiting for richer lifecycle/status screens.
- A full plugin may still be required after the MVP.
- Catalog data quality directly affects form pre-population quality.

## Non-decisions

This ADR does not decide:

- which ITSM provider is authoritative;
- Teams delegated approval authentication;
- CAB deferred scheduling implementation;
- whether SharePoint is used in the MVP.
