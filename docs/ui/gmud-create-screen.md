# GMUD create screen — UI implementation contract

> Status: **normative UI reference — F1.4 integrity cleanup**
>
> Related ADRs: [ADR-002](../adr/ADR-002-backstage-change-onramp.md) · [ADR-003](../adr/ADR-003-provider-agnostic-change-management.md) · [ADR-008](../adr/ADR-008-multi-activity-change-execution-plan.md)
>
> Visual reference: [`gmud-create-reference.jpg`](./gmud-create-reference.jpg) (composition authority; F1.3+ supersedes deployment-centric labels)

![GMUD create screen reference](./gmud-create-reference.jpg)

## Purpose

This document defines the GMUD creation screen as a **generic production change request**, not an application-deployment form.

The screen must answer:

1. **WHAT** will change?
2. **HOW** will it be executed and by whom? (F2.1.2+ — Plano de execução)
3. **WHEN** will it happen?
4. **WHAT IS THE RISK** and how can it be reversed?
5. **WHAT EVIDENCE** supports the change?

The right rail answers: **WHAT HAPPENS AFTER I CREATE IT?**

Do **not** expose universal wording that assumes Teams, Azure DevOps, Kubernetes, CI/CD pipelines, Docker images, or deployment as the only change type. Technology-specific context may appear later when actually available.

**Design rule:** every visible element must serve change management (describe, classify, evaluate risk, plan execution/reversal, support approval/audit/evidence, or communicate state). Do not add fields to fill layout space.

**Provider opacity:** ITSM provider choice (SharePoint, Jira, ServiceNow) is **not** part of the developer-facing GMUD creation experience. The developer must not choose a provider, see provider cards, understand provider routing, or know which backend provider is used. Provider selection belongs behind the Change Management capability (see ADR-003).

**Backstage first:** use Backstage/MUI form controls, theme, typography, and interaction patterns. Refine through semantics, composition, spacing, hierarchy, and proportion — not a parallel GMUD design system.

## Page shell

The screen lives inside Backstage and must preserve the normal Backstage application shell.

```text
Nova GMUD
Solicitação de mudança para produção
```

Production scope is communicated by the page subtitle. Do **not** repeat production as a read-only form field (e.g. `Ambiente = PRD`).

## Desktop layout

- **main column:** ~75–80% width — **five** numbered form sections in one InfoCard surface (F2.1.2+; four sections until F2.1.3 UI);
- **right rail:** ~20–25% — Fluxo da Mudança, Status, Identificador;
- responsive: rail stacks below form on narrow screens.

Read-only context (`Solicitante`, `Responsável`, `Referências de contexto`) is typographic (label + value) or chips — not faux outlined inputs.

## Section 1 — Detalhes da Mudança

```text
1  Detalhes da Mudança
```

Composition (do **not** force a three-column first row):

| Row | Fields |
|---|---|
| 1 | **Alvo da mudança** (catalog Component selector) · **Classificação da mudança** (`Normal` / `Emergencial`) |
| 2 | **Título da mudança** (full width) |
| 3 | **Resumo** (full width, multiline) |
| 4 | **Solicitante** · **Responsável** |
| 5 | **Referências de contexto** (read-only; when catalog provides them) |

### Alvo da mudança

- User-facing label: **Alvo da mudança** (not "Aplicação").
- F1.3 backing: Backstage Catalog **Component** entities only.
- Canonical model property: `targetRef` (e.g. `component:default/pagamentos-api`).
- Future evolution may support System, Resource, or other governed targets — not in F1.3.

### Classificação da mudança

- Required selector: **Normal** | **Emergencial**.
- Canonical model: `classification: 'normal' | 'emergency'`.
- **Initial state:** empty (`''`) — the user must explicitly choose Normal or Emergencial.
- Validation must reject an empty classification.
- Do **not** model as environment. Do **not** add Standard/other categories without approved governance.

### Removed fields (F1.3)

| Removed | Reason |
|---|---|
| Ambiente = PRD | Invariant — screen is already a production change request |
| Versão / Artefato | Deployment-specific — firewall/DB/DNS changes have no artifact version |

### Context fields

| Field | Source | Semantics |
|---|---|---|
| Solicitante | Authenticated Backstage user identity | Who is requesting the change |
| Responsável | Catalog `spec.owner` (display human-readable group/team name) | **Ownership/governance context** for the target — not the managerial approver |

### Referências de contexto (target context)

Catalog-derived metadata about the **target** — not change evidence.

Examples: repository link, build definition, catalog source link, service metadata links.

- Display as read-only chips or links when available from the selected Component.
- Helper copy when shown: references are from the catalog target, not evidence of the change itself.
- Do **not** include target context in `evidence[]` on submit in F1.4.

## Section 2 — Plano de execução (F2.1.2+ normative — UI in F2.1.3)

```text
2  Plano de execução
```

Describes **planned execution activities** — who performs each unit of work. This is **not** a workflow board, task tracker, or approval step list.

| Field | Purpose |
|---|---|
| **Título** | Short activity title |
| **Descrição** | What this activity entails |
| **Equipe executora** | Catalog **Group** responsible for the activity (`responsibleRef`) |
| **Alvo opcional** | Activity-specific Catalog **Component** when it differs from Section 1 (`targetRef`) |

Composition:

- Default: **one activity row** pre-populated (empty or with sensible starter copy).
- `[ + Adicionar atividade ]` adds rows; cap at 20 activities.
- Array order is the intended human-readable sequence — no drag-and-drop dependency graph.

**Terminology:** Section 1 **Responsável** = governance owner of the primary target (`ownerRef`). Section 2 **Equipe executora** = team expected to perform that activity (`responsibleRef`). Do not overload "Responsável" for activities.

Canonical model: `executionPlan: { activities: [{ title, description, responsibleRef, targetRef? }] }`. Server assigns `activityId` on create.

**F2.1.3 UI stop condition:** implement this section only when backend F2.1.2 is accepted. Until then, frontend may remain on four sections.

## Section 3 — Janela de Execução

```text
3  Janela de Execução
```

| Field | Purpose |
|---|---|
| Data da janela | Date the change is authorized to occur |
| Início | Window start time |
| Fim | Window end time |

This is **execution planning**, not pipeline/deployment scheduling. Canonical model: `requestedWindow: { date, startsAt, endsAt }`.

## Section 4 — Avaliação de Risco

```text
4  Avaliação de Risco
```

Stacked composition (do **not** place risk selector beside the reversal plan):

| Row | Field |
|---|---|
| 1 | **Nível de risco** (`Baixo` / `Médio` / `Alto`) — partial width on desktop |
| 2 | **Plano de reversão** (full width, multiline) |

| Field | Purpose |
|---|---|
| Nível de risco | Risk evaluation |
| Plano de reversão | What will be done if the change must be undone or fails |

Works for software, infrastructure, database, network, and manual operational changes. Canonical model property remains `rollbackPlan`.

Do not use deployment-only examples (containers, images, pipelines) in placeholders unless shown conditionally in future context.

## Section 5 — Evidências

```text
5  Evidências
```

**Change evidence** — artifacts that support evaluation and audit of **this specific change**.

Examples of change evidence (future): validation result, implementation plan, change script, PR associated with the change, test execution, approval artifact, supporting document.

**Not** target context: repository link, build definition, catalog source link, and generic service metadata belong in Section 1 **Referências de contexto**, not here.

F1.4 behavior:

- Show neutral zero-state: *"Nenhuma evidência disponível no momento. Referências e documentos de apoio poderão ser associados conforme o contexto da mudança."*
- Do **not** display catalog-derived target metadata in this section.
- Do **not** require pipeline/PR/image/test fields.
- Do **not** implement attachment storage in F1.4.
- Submit payload: `evidence: []` until real change-specific evidence exists.

## Right rail — Fluxo da Mudança

Informational only — not a workflow engine.

```text
Fluxo da Mudança

1  Aprovação do gestor
   A solicitação é avaliada pelo gestor responsável pela aprovação da mudança.

2  Validação da mudança
   A mudança e a janela solicitada são avaliadas conforme o processo de governança.

3  Autorização para execução
   Após as validações necessárias, a mudança fica autorizada para execução
   na janela aprovada.
```

Do **not** mention Teams, Adaptive Cards, Azure DevOps, CAB scheduling implementation, deploy locks, or Kubernetes.

The Catalog **Responsável** field is ownership context only — it must not be equated with the managerial approver described in step 1. Approver resolution is a later architecture concern; not implemented in F1.4.

## Right rail — Status

```text
Status
Rascunho
A GMUD ainda não foi submetida para aprovação.
```

## Right rail — Identificador

Before creation:

```text
#
changeId será gerado após criar
```

After creation: display canonical `changeId` from API response.

## Bottom actions

| Action | Label |
|---|---|
| Primary | Criar GMUD |

No secondary draft action in F1.4 — draft persistence requires a real backend model (deferred to F2+).

No `Approve` action on this screen.

## Canonical frontend model

```typescript
CreateChangeRequest {
  targetRef: string;
  classification: 'normal' | 'emergency';
  requestedBy: string;
  ownerRef?: string;
  systemRef?: string;        // optional catalog enrichment — not an editable field
  title: string;
  summary: string;
  requestedWindow: { date, startsAt, endsAt };
  risk: 'low' | 'medium' | 'high';
  rollbackPlan: string;
  evidence: ChangeEvidence[];  // F1.4: empty until change-specific evidence exists
  executionPlan: {             // F2.1.2+ — required on POST; min 1 activity
    activities: Array<{
      title: string;
      description: string;
      responsibleRef: string;  // Catalog Group
      targetRef?: string;      // optional Component
    }>;
  };
}
```

**Removed / not reintroduced:** `environment`, `artifactVersion`, `componentRef`, `provider`, `teamsUserId`, `adoApprovalId`, `cabDeferredApprovalId`

## F1.4 stop condition

After integrity cleanup: **STOP**. No backend, persistence, ITSM providers, workflow engine, provider integration, or expanded catalog target types.

## Screenshots

See [`screenshots/README.md`](./screenshots/README.md) for capture notes.
