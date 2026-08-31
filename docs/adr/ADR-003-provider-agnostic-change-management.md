# ADR-003 — Change management is provider-agnostic

- Status: Accepted (refined F2.0 architecture review)
- Date: 2026-08-27 (semantic refinement: 2026-08-30)
- Related: [ADR-006](./ADR-006-change-management-backend-contract.md), [ADR-007](./ADR-007-change-record-authority.md)

## Context

The current enterprise process may use SharePoint for GMUD data, but the platform must not be coupled to SharePoint because Jira Service Management, ServiceNow, or another ITSM may replace it.

The previous future-GMUD document assumed SharePoint as the source of truth. That assumption is superseded by this ADR.

## Decision

The platform owns a canonical change-management contract and integrates external systems through provider adapters.

Conceptually:

```text
Backstage
          |
          v
 Change Management capability
          |
          +-- platform canonical index (identity, routing, audit snapshot)
          |
          v
 canonical provider-neutral contract
          |
          v
 IChangeManagementProvider (router)
      /        |         \
 SharePoint   Jira    ServiceNow
```

See [ADR-007](./ADR-007-change-record-authority.md) for record authority: the platform owns domain authority and the canonical index; ITSM providers own operational record authority in production.

The canonical model should include stable domain concepts such as:

- `changeId`;
- title / summary;
- `targetRef` (canonical change target reference);
- requested-by / owner;
- risk/classification;
- requested execution window;
- rollback plan;
- status;
- external URL/provider metadata (internal to provider boundary — not universal form data).

Provider-specific fields must remain behind the adapter boundary.

The contract should distinguish read and write capabilities where useful, for example `IChangeReader` and `IChangeWriter`, because some providers may initially be read-only integrations.

Provider selection and configuration belong behind the Change Management capability. The developer-facing GMUD creation experience must not expose provider choice, routing, or ITSM integration details.

## SharePoint decision

SharePoint is **optional**, not a mandatory component.

A SharePoint adapter is introduced only when at least one of these is true:

1. the POC must demonstrate interoperability with the current enterprise GMUD store;
2. CAB users need a SharePoint queue/dashboard for operational reasons;
3. SharePoint is selected as the temporary authoritative change store for a real rollout.

If none of those conditions apply, the platform should not add SharePoint merely because it is available.

If a temporary POC storage provider is required, it must be explicitly labeled non-production and replaceable through the same provider interface. The F2.1 `DevelopmentProvider` is such a non-production store (ADR-007).

## Provider replaceability (precise guarantee)

**Guaranteed when the provider adapter changes:**

- Frontend create form and public HTTP contract remain stable
- New changes use the configured default provider
- Domain types do not embed provider-specific fields
- Backend adapter + config change only — no frontend rewrite

**Not guaranteed:**

- Automatic survival of all historical records if an old provider is decommissioned
- Zero-downtime bulk migration between ITSM systems
- Identical workflow semantics across providers

**Historical reads** require immutable per-change `providerKey` routing and a concurrent provider registry — not automatic record portability. See ADR-007.

Provider-agnosticism means the **developer-facing contract** does not change when ITSM changes. It does **not** mean switching production ITSM tomorrow with zero migration effort.

## Correlation contract

When a change is executed through an Azure DevOps pipeline, the pipeline **may** carry the stable change identifier for correlation, for example:

```text
changeId = CHG-2026-004182
```

Azure DevOps / CD is one **optional** future execution path — not every GMUD implies pipeline → deployment.

Where an Approval Gateway or interaction channel is used, it resolves change context through the provider without duplicating the entire change record into external system variables.

## Consequences

- Swapping ITSM providers does not change Backstage frontend templates or the public HTTP create/get contract.
- Power Automate must not become the core orchestration engine for the platform.
- Provider adapters can differ in authentication, schema, and lifecycle while exposing one canonical change contract.
- The old SharePoint-specific future design must be revised or marked superseded.
- Platform must persist a canonical index (identity + routing + audit snapshot) — see ADR-007 — separate from ITSM operational records.
