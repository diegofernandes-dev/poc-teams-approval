# GMUD create screen — UI implementation contract

> Status: **normative UI reference — F1.3 semantic revision**
>
> Related ADR: [`ADR-002 — Backstage is the change-request onramp`](../adr/ADR-002-backstage-change-onramp.md)
>
> Visual reference: [`gmud-create-reference.jpg`](./gmud-create-reference.jpg) (composition authority; F1.3 supersedes deployment-centric labels)

![GMUD create screen reference](./gmud-create-reference.jpg)

## Purpose

This document defines the GMUD creation screen as a **generic production change request**, not an application-deployment form.

The screen must answer:

1. **WHAT** will change?
2. **WHEN** will it happen?
3. **WHAT IS THE RISK** and how can it be reversed?
4. **WHAT EVIDENCE** supports the change?

The right rail answers: **WHAT HAPPENS AFTER I CREATE IT?**

Do **not** expose universal wording that assumes Teams, Azure DevOps, Kubernetes, CI/CD pipelines, Docker images, or deployment as the only change type. Technology-specific context may appear later when actually available.

**F1.3 design rule:** every visible element must serve change management (describe, classify, evaluate risk, plan execution/reversal, support approval/audit/evidence, or communicate state). Do not add fields to fill layout space.

**Backstage first:** use Backstage/MUI form controls, theme, typography, and interaction patterns. Refine through semantics, composition, spacing, hierarchy, and proportion — not a parallel GMUD design system.

## Page shell

The screen lives inside Backstage and must preserve the normal Backstage application shell.

```text
Nova GMUD
Solicitação de mudança para produção
```

Production scope is communicated by the page subtitle. Do **not** repeat production as a read-only form field (e.g. `Ambiente = PRD`).

## Desktop layout

- **main column:** ~75–80% width — numbered form sections in one InfoCard surface;
- **right rail:** ~20–25% — Fluxo da Mudança, Status, Identificador;
- responsive: rail stacks below form on narrow screens.

Read-only context (`Solicitante`, `Responsável`) is typographic (label + value), not faux outlined inputs.

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

### Alvo da mudança

- User-facing label: **Alvo da mudança** (not "Aplicação").
- F1.3 backing: Backstage Catalog **Component** entities only.
- Canonical model property: `targetRef` (e.g. `component:default/pagamentos-api`).
- Future evolution may support System, Resource, or other governed targets — not in F1.3.

### Classificação da mudança

- Required selector: **Normal** | **Emergencial**.
- Canonical model: `classification: 'normal' | 'emergency'`.
- Do **not** model as environment. Do **not** add Standard/other categories without approved governance.

### Removed fields (F1.3)

| Removed | Reason |
|---|---|
| Ambiente = PRD | Invariant — screen is already a production change request |
| Versão / Artefato | Deployment-specific — firewall/DB/DNS changes have no artifact version |

### Context fields

| Field | Source |
|---|---|
| Solicitante | Authenticated Backstage user identity |
| Responsável | Catalog `spec.owner` (display human-readable group/team name) |

## Section 2 — Janela de Execução

```text
2  Janela de Execução
```

| Field | Purpose |
|---|---|
| Data da janela | Date the change is authorized to occur |
| Início | Window start time |
| Fim | Window end time |

This is **execution planning**, not pipeline/deployment scheduling. Canonical model: `requestedWindow: { date, startsAt, endsAt }`.

## Section 3 — Avaliação de Risco

```text
3  Avaliação de Risco
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

## Section 4 — Evidências

```text
4  Evidências
```

Supporting evidence for evaluation and audit — **not** CI/CD metadata by definition.

F1.3 behavior:

- Show catalog-derived references when available (links, annotations).
- Neutral zero-state when none exist: *"Nenhuma evidência disponível no momento. Referências e documentos de apoio poderão ser associados conforme o contexto da mudança."*
- Do **not** require pipeline/PR/image/test fields.
- Do **not** implement attachment storage in F1.3.

## Section 5 — Integração de GMUD

**Not implemented in F1.** Provider integration (SharePoint/Jira/ServiceNow) is architectural visualization only for future phases.

## Right rail — Fluxo da Mudança

Informational only — not a workflow engine.

```text
Fluxo da Mudança

1  Aprovação do responsável
   A solicitação é avaliada pelo responsável definido para a mudança.

2  Validação da mudança
   A mudança e a janela solicitada são avaliadas conforme o processo de governança.

3  Autorização para execução
   Após as validações necessárias, a mudança fica autorizada para execução
   na janela aprovada.
```

Do **not** mention Teams, Adaptive Cards, Azure DevOps, CAB scheduling implementation, deploy locks, or Kubernetes.

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
| Secondary | Salvar rascunho |
| Primary | Criar GMUD |

No `Approve` action on this screen.

## Canonical frontend model (F1.3)

```typescript
CreateChangeRequest {
  targetRef: string;
  classification: 'normal' | 'emergency';
  requestedBy: string;
  ownerRef?: string;
  systemRef?: string;        // optional catalog enrichment
  title: string;
  summary: string;
  requestedWindow: { date, startsAt, endsAt };
  risk: 'low' | 'medium' | 'high';
  rollbackPlan: string;
  evidence: ChangeEvidence[];
}
```

**Removed:** `environment`, `artifactVersion`, `componentRef`

## F1.3 stop condition

After semantic revision: **STOP**. No backend, conditional GMUD types, workflow engine, provider integration, or expanded catalog target types.

## Screenshots

See [`screenshots/README.md`](./screenshots/README.md) for F1.3 capture notes.
