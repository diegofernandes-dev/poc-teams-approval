# GMUD my changes screen — UI implementation contract

> Status: **normative UI reference — F2.2 discovery surface, F2.2.1 participant read policy**
>
> Related ADRs: [ADR-006](../adr/ADR-006-change-management-backend-contract.md) ("Participant read scope (F2.2.1)") · [ADR-007](../adr/ADR-007-change-record-authority.md) ("Clarification — discovery/listing vs. detail authority") · [ADR-008](../adr/ADR-008-multi-activity-change-execution-plan.md) (`responsibleRef`)
>
> Related UI: [`gmud-create-screen.md`](./gmud-create-screen.md) · [`gmud-detail-screen.md`](./gmud-detail-screen.md)

## Purpose

This is the entry point of the GMUD capability at `/gmud`. It answers the question
a user must never have to ask after creating a GMUD:

**"Where did my GMUD go?"**

The screen must answer:

1. **WHICH** GMUDs am I allowed to see? As of F2.2.1 this means: GMUDs I requested,
   GMUDs owned by a team I belong to, and GMUDs where a team I belong to is the
   responsible executor of at least one activity — i.e. GMUDs I **participate**
   in. The label "Minhas GMUDs" is kept unchanged; it now reads as "GMUDs I
   participate in" rather than only "GMUDs I own or requested".
2. **WHAT** is each one, at a glance (title, classification, status, window, target, responsible)?
3. **HOW** do I open one, or start a new one?

This is a **discovery surface, not enterprise search**. Do not add a search input,
filter drawer, status chips as filters, date ranges, sorting frameworks, or a
server-side query language. Default order — `createdAt` descending — is sufficient.

**Design rule:** a compact table/list, not decorative cards. Match the flat,
low-elevation surface style already established by the create screen
(`gmudCreateStyles.ts` — `boxShadow: none`, `1px solid divider`).

**Provider opacity:** exactly as in the create screen — no provider name,
`providerKey`, `externalId`, or routing detail ever appears here. A row shows only
what `ChangeSummary` carries.

## Page shell

```text
Header
  Minhas GMUDs                              [ Nova GMUD ]
  Solicitações de mudança

Content
  <Table> or <EmptyState>
```

`Page themeId="home"` → `Header` (title + primary action) → `Content` — same shell
pattern as the create screen, reused via a page-local `GmudListShell`.

## List content

Backed by `GET /api/change-management/changes` → `ChangeSummary[]`, itself backed by
the **platform canonical index creation-time snapshot** — not a live read through
any `IChangeManagementProvider`. See ADR-007's list-vs-detail table:

| Surface | Source | Authority |
|---|---|---|
| This screen | `change_index` snapshot | Discovery only |
| Detail screen | `providerKey` → provider | Operational record authority |

Only records the current actor is allowed to read are returned — the backend
predicate is identical to the one `GET /:changeId` uses (own changes, owner-group
changes, or `platform_admin`). A user cannot discover a row here that a direct GET
would refuse.

### Row / columns

| Column | Source | Notes |
|---|---|---|
| Identificador | `changeId` | Link to `/gmud/:changeId` |
| Título | `title` | — |
| Classificação | `classification` | `CLASSIFICATION_LABELS` |
| Status | `status` | Same outlined `Chip` + `STATUS_LABELS` pattern as the create-page success card. Currently `Submetida` only. |
| Janela de execução | `requestedWindow` | `formatChangeWindow()` — localized start–end + timezone |
| Alvo | `targetRef` | Catalog display name when resolvable, else the raw ref |
| Responsável | `ownerRef` | Catalog display name when resolvable, else the raw ref |
| Criada em | `createdAt` | Localized date |

**Must never render:** `providerKey`, `externalId`, `externalUrl`, ServiceNow/Jira/SharePoint
data, idempotency information, or any internal persistence state. `ChangeSummary`
does not carry these fields, so this is enforced structurally, not just by
convention.

**Must never render the full execution plan** — only `activityCount` exists on the
summary; the ordered plan is detail-screen content (see
[`gmud-detail-screen.md`](./gmud-detail-screen.md)).

### Display name resolution

`targetRef` and `ownerRef` are persisted historical refs — they remain authoritative
even if the Catalog changes. The UI may resolve a friendlier display name via the
Catalog, but:

- resolution is best-effort and asynchronous;
- failure to resolve, or the Catalog being unavailable, **must not** make the row
  disappear or the GMUD unreadable;
- the raw entity ref is the permanent fallback, not a loading placeholder.

## Empty state

```text
Nenhuma GMUD encontrada

Você ainda não possui solicitações de mudança disponíveis.

[ Criar GMUD ]
```

Shown when the actor has zero readable changes. The action button navigates to
`/gmud/new`.

## Error state

If `GET /changes` itself fails (rare — see "Provider failure" below for why this
list stays available even when a provider is down), show a `WarningPanel` with a
human-safe message from the read-flavored error mapping (`mapChangeReadErrorToMessage`)
— never the raw backend error.

## Provider failure during list

Because this list reads only from the canonical index, **it remains available even
if a configured ITSM provider is temporarily unavailable** — listing never calls
`IChangeManagementProvider`. Opening a specific row may then return a provider
failure on the detail screen; that is expected and documented there, not here.

## Bottom / row actions

| Action | Label | Target |
|---|---|---|
| Header primary | Nova GMUD | `/gmud/new` |
| Row | (identifier link) | `/gmud/:changeId` |

No edit, delete, or bulk actions. No per-row status transitions — the list is a
read surface, not a workflow board.

## Canonical frontend model

```typescript
ChangeSummary {
  changeId: string;
  title: string;
  classification: 'normal' | 'emergency';
  status: 'submitted';
  targetRef: string;
  ownerRef?: string;
  requestedBy: string;
  requestedWindow: { startsAtUtc: string; endsAtUtc: string; timezone: string };
  activityCount: number;
  createdAt: string;
}
```

**Not present, by design:** `providerKey`, `externalId`, `externalUrl`, `evidence`,
`executionPlan` (full), `rollbackPlan`, `risk`, `summary`, `systemRef` — those are
detail-screen fields, kept out of the discovery payload deliberately.

## F2.2 stop condition

After this checkpoint: **STOP**. No search, filters, sort controls, pagination
framework beyond a fixed limit, status-as-filter, or cross-actor/team-wide listing.
Those require an F3 architecture decision.

## Screenshots

Not yet captured — see [`screenshots/README.md`](./screenshots/README.md) for
capture conventions when a baseline is taken.
