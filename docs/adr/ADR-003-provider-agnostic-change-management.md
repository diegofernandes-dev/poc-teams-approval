# ADR-003 — Change management is provider-agnostic

- Status: Accepted
- Date: 2026-08-27

## Context

The current enterprise process may use SharePoint for GMUD data, but the platform must not be coupled to SharePoint because Jira Service Management, ServiceNow, or another ITSM may replace it.

The previous future-GMUD document assumed SharePoint as the source of truth. That assumption is superseded by this ADR.

## Decision

The platform owns a canonical change-management contract and integrates external systems through provider adapters.

Conceptually:

```text
Backstage / Approval Gateway
          |
          v
 Change Management Contract
          |
          v
 IChangeManagementProvider
      /        |         \
 SharePoint   Jira    ServiceNow
```

The canonical model should include stable domain concepts such as:

- `changeId`;
- title / summary;
- application/component reference;
- requested-by / owner;
- risk/classification;
- requested deployment window;
- rollback plan;
- status;
- external URL/provider metadata.

Provider-specific fields must remain behind the adapter boundary.

The contract should distinguish read and write capabilities where useful, for example `IChangeReader` and `IChangeWriter`, because some providers may initially be read-only integrations.

## SharePoint decision

SharePoint is **optional**, not a mandatory MVP component.

A SharePoint adapter/plugin is introduced only when at least one of these is true:

1. the POC must demonstrate interoperability with the current enterprise GMUD store;
2. CAB users need a SharePoint queue/dashboard for operational reasons;
3. SharePoint is selected as the temporary authoritative change store for a real rollout.

If none of those conditions apply, the MVP should not add SharePoint merely because it is available.

If a temporary POC storage provider is required, it must be explicitly labeled non-production and replaceable through the same provider interface.

## Correlation contract

The pipeline carries only the stable change identifier, for example:

```text
changeId = CHG-2026-004182
```

The Approval Gateway resolves change context through the provider and enriches Teams cards without duplicating the entire change record into Azure DevOps variables.

## Consequences

- Migrating SharePoint -> Jira/ServiceNow does not change Backstage templates or approval semantics.
- Power Automate must not become the core orchestration engine for the platform.
- Provider adapters can differ in authentication, schema, and lifecycle while exposing one canonical change contract.
- The old SharePoint-specific future design must be revised or marked superseded.
