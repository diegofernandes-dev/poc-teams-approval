# Architect decision brief — Teams approval identity

> Status: **awaiting architect decision**.
>
> Date: 2026-08-26
>
> Audience: solution / security architect reviewing the Approval Gateway POC.

## Purpose

Document the POC state after the first end-to-end approval attempt, the identity problem discovered when applying approvals from Teams, and the viable options so an architect can choose the next path.

This document does **not** choose the production approach. It records facts and trade-offs.

## Non-negotiable constraints (already agreed for the POC)

1. **Azure DevOps is the approval authority** — approvers, pending state, authorization, environment protection, and audit live in ADO.
2. **Teams is UI only** — notification and interaction surface; not a second approval system.
3. **Card payload is untrusted** — `approvalId` / action data are correlation hints only; gateway must re-read ADO before any decision.
4. **No fail-open** — Teams/gateway failure must leave the ADO approval pending.
5. **HML-only runs must not create PRD approval activity** — PRD stage is compile-time gated (`${{ if eq(parameters.promoteToPrd, true) }}`).

## What the POC has proven

| Capability | Result |
|---|---|
| Pipeline HML-only (PRD absent from graph) | Proven (run 127) |
| Pipeline with `promoteToPrd=true` → Environment approval on `prd-teams-poc` | Proven |
| Service Hook `approval-pending` → Function webhook | Proven |
| Persist Teams conversation reference in Blob (Flex scale-out safe) | Proven |
| Proactive Adaptive Card to personal Teams chat | Proven |
| Gateway re-reads ADO approval + Environment approver policy | Proven in code / tests |
| Apply approval via REST `PATCH /_apis/pipelines/approvals` | Proven — **with wrong audit identity** (see below) |

### Runtime inventory (POC)

- Function App: `func-ado-teams-poc-diegolab`
- Host: `https://func-ado-teams-poc-diegolab-b5crbkdncmcqb6a6.eastus2-01.azurewebsites.net`
- Azure Bot + Teams personal app installed
- Environment: `prd-teams-poc` (approvers include `approver@diegolab.onmicrosoft.com` and Diego for testing)
- Service Hook: `approval-pending`, filtered to `prd-teams-poc`

## The identity problem (blocker for in-Teams approve)

### Symptom

User clicked Approve in Teams as **Approver POC**. Azure DevOps audit showed:

```text
Diego Fernandes
Approved • …
Approved from Microsoft Teams (Approval Gateway POC).
```

### Root cause

`PATCH` Approvals authenticates with a **single service PAT** (`AzureDevOps__Pat`, Diego’s token). Azure DevOps records the **token owner**, not the Teams clicker. There is **no API field** to attribute the approval to another user while authenticating as a service account.

```text
Teams click (Approver POC)
        |
        v
Gateway validates caller against Environment approvers
        |
        v
PATCH Approvals with Diego PAT
        |
        v
ADO audit = Diego Fernandes   ← wrong
```

This is an Azure DevOps platform behavior, not a Teams bug.

### Rejected approach (do not pursue)

**Per-approver PATs stored in the gateway** (map email → PAT, PATCH with that PAT).

Why rejected for this org/POC:

- Gateway would hold long-lived credentials of every human approver
- Operationally and security-wise unacceptable at scale
- Does not match “user already logged into Teams” expectation

## Current code posture (after correction)

To avoid shipping a false “approve in Teams” UX that lies about who approved:

1. **Adaptive Card** for real approvals uses **`Action.OpenUrl`** → “Review in Azure DevOps” (run results URL).
2. Gateway **does not apply** approvals with the service PAT.
3. Service PAT remains **read-only** (GET approval / Environment policy) when needed.
4. Legacy `Action.Execute` approve/reject paths (old cards / POC fake card) either acknowledge locally or tell the user to use the ADO link for real approvals.
5. Client API shape allows a future **Bearer user token** on `UpdateApprovalAsync` (delegated auth), without storing approver PATs.

Deployed behavior matches this posture.

## Options for architect decision

### Option A — Deep link only (current POC default)

**UX:** Teams notifies → user opens ADO → approves with their own ADO session.

| Pros | Cons |
|---|---|
| Correct audit immediately | Leaves Teams for the decision |
| Minimal Entra / Bot complexity | Weak “approve in chat” story |
| No delegated token plumbing | |

**Fit:** notification POC; prove correlation and delivery only.

---

### Option B — Teams SSO + OAuth On-Behalf-Of (OBO) to Azure DevOps *(recommended candidate for in-Teams approve)*

**UX:** User clicks Approve in Teams → silent SSO (after one-time consent) → gateway exchanges for ADO user token → `PATCH` with `Authorization: Bearer` → audit shows the clicker.

```text
Teams (user already signed in to Entra)
        |
   Bot SSO / OAuth connection
        |
   Access token for bot API (user)
        |
   OBO exchange
        |
   Azure DevOps token (delegated, user)
        |
   PATCH Approvals
        |
   ADO audit = that user
```

Required platform work (not done):

- Entra App Registration: Azure DevOps **delegated** permission (e.g. `vso.pipelineresources_use` / `.default`)
- Azure Bot: OAuth connection (AAD v2) + token exchange URL
- Teams manifest: `webApplicationInfo`, `validDomains` including `token.botframework.com`
- Gateway: obtain user token on `Action.Execute`, call ADO with Bearer
- Consent: first use may prompt; admin consent may be preferred in production

| Pros | Cons |
|---|---|
| Keeps decision inside Teams | Non-trivial Entra + Bot + manifest work |
| Correct ADO audit | One-time consent / admin consent |
| No human PATs in gateway | OBO failure modes and token refresh to design |
| Aligns with Microsoft guidance for bots calling APIs as the user | Larger POC surface |

**Fit:** product direction if “approve without leaving Teams” is a hard requirement.

---

### Option C — Hybrid

Card always deep-links for authority (A), plus optional in-Teams approve after SSO (B) when token available.

| Pros | Cons |
|---|---|
| Fallback if SSO fails | Two interaction paths to support |
| Safer progressive delivery | More UX/copy complexity |

---

### Option D — Out of scope / not recommended

- Service principal / managed identity applying approvals “as the system” while faking human identity — **not supported** for correct human audit.
- Trusting Teams display name alone as authorization without ADO token — identity matching is useful for UX gates, **not** for who ADO records as approver.

## Explicit non-goals for this decision

- GMUD / SharePoint enrichment (see `docs/future-gmud-context-enrichment.md`)
- `approval-completed` Service Hook / card lifecycle updates
- Multi-approver routing directories / group chat
- Publishing Teams app to org catalog

## Recommendation input for the architect (not a decision)

From the engineering POC perspective:

- If the product must keep **approve/reject inside Teams with correct ADO audit**, **Option B** is the only scalable, honest path.
- If the near-term goal is only **reliable notification + correct authority**, keep **Option A** and stop claiming in-Teams approval until B lands.
- Do **not** reintroduce per-approver PATs.

## Decision requested

Please choose one:

1. **Stay on Option A** (deep link) for the remainder of the POC; document as intentional UX.
2. **Authorize Option B** (Teams SSO + OBO → ADO) as the next implementation slice, including Entra/Bot/manifest changes.
3. **Authorize Option C** (hybrid) with an explicit MVP order (A first, then B).

Record the decision below when made:

```text
Decision:     (A / B / C)
Date:         …
Decided by:   …
Notes:        …
```

## Related code / commits

- Service Hook + Adaptive Card + ADO client path landed under commits culminating in `afc4e87` (in-Teams PATCH with service PAT — demonstrated the identity bug).
- Follow-up on this branch: remove service-PAT apply path; OpenUrl deep link; leave Bearer update hook for Option B.
