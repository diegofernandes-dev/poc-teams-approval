# ADR-004 — Teams approvals use delegated Azure DevOps user identity

- Status: Proposed historical design — proposed supersession by ADR-009
- Date: 2026-08-27

> **F3.0 target:** [ADR-009](./ADR-009-change-authorization-model.md) keeps Teams as
> an interaction channel but routes human decisions to Change Management rather
> than patching an Azure DevOps approval object. If ADR-009 is accepted, this
> delegated Teams-to-ADO design is not the target authorization path.

## Context

The POC proved that applying an Azure DevOps approval with a shared/service PAT produces the wrong human audit identity. Azure DevOps records the token owner, not the Teams user who clicked the card.

Per-user PAT storage is rejected. A service identity pretending to be a human approver is also rejected.

The desired UX is to approve/reject from Teams while Azure DevOps records the real user and applies that user's own authorization.

## Proposed decision

Use the Teams user's delegated Microsoft Entra identity to obtain an Azure DevOps access token and perform the approval REST call as that user.

For new integrations, use Microsoft Entra ID OAuth rather than the deprecated Azure DevOps OAuth platform.

Preferred implementation order:

1. use Microsoft 365 Agents SDK / Azure Bot user authorization abstractions where possible;
2. prove acquisition of a delegated Azure DevOps token for the signed-in Teams user;
3. perform a read-only approval query with that token;
4. validate Azure DevOps permissions for the current user (`$expand=permissions` when applicable);
5. add approval write permission;
6. PATCH the approval with the delegated token;
7. verify Azure DevOps audit / `actualApprover` is the real clicker;
8. verify a non-approver cannot approve.

A deep link to Azure DevOps remains the fail-safe fallback when delegated authentication is unavailable.

## Security model

- Teams authenticates the caller.
- Azure DevOps authorizes the approval.
- Gateway-side display-name matching is not sufficient authorization.
- Card payloads are untrusted correlation data.
- The gateway must re-read the current approval before action.
- The gateway must not persist long-lived human credentials or PATs.

## Why status is not Accepted yet

The architecture is preferred, but the POC has not yet proven the full delegated-token path with a real Azure DevOps approval PATCH and correct human audit.

The next auth checkpoint must therefore be a proof, not production implementation.

## Acceptance criteria

This ADR can become Accepted only when all are demonstrated:

- Teams user obtains delegated Azure DevOps token through supported Microsoft Entra/Bot mechanisms;
- approval read works under that identity;
- authorized approver can PATCH a pending Environment approval;
- Azure DevOps records the clicker as the real approver;
- unauthorized user is rejected by Azure DevOps;
- auth failure leaves the approval pending and provides the Azure DevOps fallback link.

## Consequences

Until accepted, the production-safe behavior remains "open/review in Azure DevOps" rather than falsely attributing an approval applied with a service credential.
