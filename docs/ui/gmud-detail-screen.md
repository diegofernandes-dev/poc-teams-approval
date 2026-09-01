# GMUD detail screen — UI implementation contract

> Status: **normative UI reference — F2.2 read surface**
>
> Related ADRs: [ADR-006](../adr/ADR-006-change-management-backend-contract.md) · [ADR-007](../adr/ADR-007-change-record-authority.md) ("Clarification — discovery/listing vs. detail authority") · [ADR-008](../adr/ADR-008-multi-activity-change-execution-plan.md) (execution plan domain)
>
> Related UI: [`gmud-create-screen.md`](./gmud-create-screen.md) · [`gmud-my-changes-screen.md`](./gmud-my-changes-screen.md)

## Purpose

Reached from the my-changes list or directly from the post-create success screen
(`Ver GMUD`), this screen answers:

1. **WHAT** is the full canonical record for this GMUD?
2. **WHO** requested it, and who owns it?
3. **WHAT** is the execution plan, in order?
4. **WHEN** will it run, and **WHAT IS THE RISK**?

This is a **read surface, not an editor**. No field on this screen is editable. No
approve/reject/execute action exists here — those require future workflow
architecture, explicitly out of scope for this checkpoint.

## Data source — provider-authoritative, unchanged routing

Unlike the list screen, detail continues to use the existing provider-routed read
path, unchanged by F2.2:

```text
GET /changes/:changeId
    ↓
change_index (lookup by changeId, must be finalized)
    ↓
stored providerKey (immutable, assigned at create)
    ↓
original provider (IChangeManagementProvider.get)
    ↓
canonical Change (public shape)
```

The platform snapshot inside `change_index` is **not** returned to this screen in
the normal path — only used internally for the 503 fallback message (see below).
Do not read this screen directly from `change_index`.

## Page shell

```text
Header
  <change.title>
  Detalhe da GMUD

Content
  <changeId> card (status, classification)
  Contexto card (alvo, solicitante, responsável, sistema)
  Plano de execução card (ordered list)
  Janela de execução card
  Risco e reversão card
  Evidências card
  [ Voltar para Minhas GMUDs ]
```

Each section is a separate `InfoCard` using the same flat surface style as the
create screen (`formSurface` class) — not one monolithic form.

## Section — header

| Field | Source |
|---|---|
| `changeId` | Card title |
| Título | `change.title` |
| Status | `STATUS_LABELS[change.status]` — outlined `Chip`, same pattern as create-page success card. Currently `Submetida` only |
| Classificação | `CLASSIFICATION_LABELS[change.classification]` |

## Section — contexto

| Field | Source | Resolution |
|---|---|---|
| Alvo da mudança | `change.targetRef` | Catalog display name, ref fallback |
| Solicitante | `change.requestedBy` | Catalog display name, ref fallback |
| Responsável | `change.ownerRef` | Catalog display name, ref fallback |
| Sistema | `change.systemRef` (optional) | Shown only when present |

Same rule as the list screen: a persisted ref is authoritative; failed or pending
name resolution falls back to the raw ref and never blocks rendering the section.

## Section — plano de execução

Renders `change.executionPlan.activities` as a **readable ordered plan**, array
order = execution order:

```text
Plano de execução

1. Criar database
   Executar deploy da correção na janela autorizada.
   Equipe executora: DBA

2. Liberar firewall
   Abrir regras de firewall necessárias.
   Equipe executora: Network

3. Implantar aplicação
   Executar deploy da correção.
   Equipe executora: Payments
   Alvo: payments-api
```

| Field | Source | Notes |
|---|---|---|
| Ordinal + título | `index + 1`, `activity.title` | Order is array order — no reordering UI |
| Descrição | `activity.description` | — |
| Equipe executora | `activity.responsibleRef` | Catalog display name, ref fallback |
| Alvo (optional) | `activity.targetRef` | Rendered only when present |

**Must render as an ordered, immutable plan** — not Kanban cards, not
draggable/reorderable nodes, not per-activity status/task rows, not approval items.
There is no per-activity status field on the domain model; do not invent one on the
UI side.

Zero state: "Nenhuma atividade registrada." (defensive only — the backend requires
at least one activity on create; this should not occur in practice).

## Section — janela de execução

`formatChangeWindow(change.requestedWindow)` — localized start–end instant plus the
stored timezone label. Uses the same UTC-stored / display-localized model as the
create screen's window fields, formatted once for this read context.

## Section — risco e reversão

| Field | Source |
|---|---|
| Nível de risco | `RISK_LABELS[change.risk]` |
| Plano de reversão | `change.rollbackPlan` |

## Section — evidências

Current evidence representation is a zero state — `evidence` is always `[]` on the
canonical model today (per ADR-006, F2.0 accepts `evidence: []` only on create; no
evidence upload has been implemented since). Show: "Nenhuma evidência anexada."

Do not implement attachment upload/download here — same exclusion as the create
screen's Evidências section.

## Bottom actions

| Action | Label | Target |
|---|---|---|
| Secondary | Voltar para Minhas GMUDs | `/gmud` |

No edit, no delete, no approve/reject, no execute.

## Error states

| Backend response | UI behavior |
|---|---|
| `404 NOT_FOUND` | `WarningPanel` — "GMUD não encontrada." No canonical fields rendered. |
| `403 FORBIDDEN` / `401 UNAUTHORIZED` | `WarningPanel` — "Você não tem permissão para consultar esta GMUD." |
| `503 PROVIDER_UNAVAILABLE` | `WarningPanel` — "Não foi possível consultar os dados completos da GMUD no momento. Tente novamente." |

**On 503, do not silently show the platform snapshot as if it were current
provider data.** The screen shows only the warning message — no partial change
fields, no stale data presented as live. This is a deliberate F2.2 constraint, not
an oversight to "improve" with a degraded-detail view; that would require a
separate architecture decision.

Because the my-changes list is index-backed (see
[`gmud-my-changes-screen.md`](./gmud-my-changes-screen.md)), a provider outage is
expected to look like: list works normally, this screen shows the 503 message for
the affected record only.

## Canonical frontend model

```typescript
Change {
  changeId: string;
  status: 'submitted';
  targetRef: string;
  classification: 'normal' | 'emergency';
  requestedBy: string;
  ownerRef?: string;
  systemRef?: string;
  title: string;
  summary: string;
  requestedWindow: { startsAtUtc: string; endsAtUtc: string; timezone: string };
  risk: 'low' | 'medium' | 'high';
  rollbackPlan: string;
  evidence: ChangeEvidence[];        // always [] today
  executionPlan: {
    activities: Array<{
      activityId: string;
      title: string;
      description: string;
      responsibleRef: string;
      targetRef?: string;
    }>;
  };
  createdAt: string;
}
```

Identical to the backend `Change` type — `ProviderReference` is **not** part of
this model and must never appear in this screen's props, DOM, or network payload.

## Note on ADR-008

`executionPlan` above was introduced in ADO commit `5e4f30e` per
[ADR-008](../adr/ADR-008-multi-activity-change-execution-plan.md), accepted at the
F2.1.2 architecture review (2026-09-01). This screen's execution plan rendering
rules (immutable, ordered, no per-activity status) follow that ADR's terminology
and constraints directly — see `implementation-progress.md` §14 for the checkpoint.

## F2.2 stop condition

After this checkpoint: **STOP**. No editing, no per-activity status, no
approve/reject/execute actions, no attachment upload, no re-ordering UI for the
execution plan.

## Screenshots

Not yet captured — see [`screenshots/README.md`](./screenshots/README.md) for
capture conventions when a baseline is taken.
