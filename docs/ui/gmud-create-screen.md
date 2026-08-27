# GMUD create screen — MVP UI implementation contract

> Status: **normative UI reference for the MVP**
>
> Related ADR: [`ADR-002 — Backstage is the change-request onramp`](../adr/ADR-002-backstage-change-onramp.md)
>
> Visual reference: [`gmud-create-reference.jpg`](./gmud-create-reference.jpg)

![GMUD create screen reference](./gmud-create-reference.jpg)

## Purpose

This document exists to prevent implementation agents from inventing a different UX for the GMUD creation screen.

The screenshot above defines the target **composition, hierarchy, section ordering, side-panel structure, labels, and main actions**. The implementation should look recognizably like this reference while still using maintainable Backstage components and the active Backstage theme.

Do **not** reproduce the screenshot as a static image. Implement a real responsive form that preserves the same visual structure.

## Page shell

The screen lives inside Backstage and must preserve the normal Backstage application shell.

Expected desktop composition:

```text
+----------------------+-----------------------------------------------+
| Backstage sidebar    | Top navigation / search / current user       |
|                      +-----------------------------------------------+
| Home                 |                                               |
| Catalog              | Nova GMUD                         Right rail  |
| APIs                 | Solicitação de mudança...                    |
| TechDocs             |                                               |
| Create               | Main form cards                  Approval    |
| Pipelines            |                                   flow       |
| GMUD  <- selected    |                                   Status     |
|                      |                                   ID         |
| Admin                |                                               |
+----------------------+-----------------------------------------------+
```

The `GMUD` navigation entry is selected in the left sidebar.

The page header is:

```text
Nova GMUD
Solicitação de mudança para produção
```

## Desktop layout

Use a two-column content layout below the page title:

- **main column:** approximately 75–80% of the content width;
- **right rail:** approximately 20–25%;
- consistent 16–24 px gaps;
- light Backstage page background;
- white cards with subtle border/shadow and modest corner radius.

The main column contains five numbered form sections. The right rail contains three stacked summary cards.

The visual reference is 16:9 desktop. Responsive behavior may stack the right rail below the form on narrower screens, but desktop must remain two-column.

## Section 1 — Detalhes da Mudança

Header:

```text
1  Detalhes da Mudança
```

First row, three fields:

| Field | Reference value | Behavior |
|---|---|---|
| Aplicação | `pagamentos-api` | Prefer catalog-derived selector/read-only context |
| Ambiente | `PRD` | MVP target is production |
| Versão / Artefato | `3.8.2` | Prefer pipeline/artifact-derived value when available |

Second row:

```text
Título da mudança
[ Ex.: Atualização de dependências e correções de segurança                    ]
```

Third row:

```text
Resumo
[ Descreva o objetivo da mudança, o que será alterado e o impacto esperado... ]
[                                                                             ]
```

Fourth row, two fields:

| Field | Reference value |
|---|---|
| Solicitante | `João Silva` |
| Owner do sistema | `Payments Platform` |

`Solicitante` should normally come from the authenticated Backstage user. `Owner do sistema` should preferentially come from Software Catalog ownership metadata rather than free text.

## Section 2 — Janela de Implantação

Header:

```text
2  Janela de Implantação
```

Three horizontal fields on desktop:

```text
Data da janela       Início da janela       Fim da janela
27/08/2026           22:00                  23:00
```

Use date/time controls consistent with the Backstage/MUI stack.

The requested window is business/change context. It must not be confused with the technical CAB deferred-approval implementation, which remains governed by ADR-005.

## Section 3 — Risco e Rollback

Header:

```text
3  Risco e Rollback
```

Desktop composition:

- left third: `Nível de risco` selector;
- right two-thirds: multiline `Plano de rollback`.

Reference risk:

```text
Médio
```

Reference rollback copy demonstrates the expected level of detail:

```text
Reverter para a imagem anterior (3.8.1) via pipeline de rollback.
Validação de saúde dos serviços e monitoramento por 30 minutos.
Caso falhe, acionar plano de contingência.
```

Do not hard-code these example values.

## Section 4 — Evidências

Header:

```text
4  Evidências
```

Display evidence as compact chips/buttons with icons and, where appropriate, external-link affordances.

Reference items:

```text
Pipeline #8271
PR #142
Image: sha256:ab...
Testes aprovados
```

The implementation should prefer metadata already known by Backstage / Azure DevOps / the deployment context instead of asking the developer to paste every link manually.

## Section 5 — Integração de GMUD

Header:

```text
5  Integração de GMUD
```

Helper text should communicate the architectural decision, not SharePoint coupling. Suggested copy:

```text
Sistema de change é plugável e agnóstico ao provedor. Selecione o destino para integração.
```

The visual reference shows three provider cards/options:

```text
[ SharePoint — Selecionado ]
[ Jira Service Management ]
[ ServiceNow ]
```

### Important architectural rule

These provider choices are primarily an **architectural visualization**. Do not automatically expose a provider selector to ordinary developers if the deployment has one centrally configured provider.

Preferred production behavior:

```text
ChangeManagement.Provider = centrally configured
```

If only one provider is enabled, show it as informational context or omit the selector entirely. The developer should not have to understand infrastructure/provider routing.

The MVP may keep the provider selector only when explicitly useful for the POC.

## Right rail — Fluxo de Aprovação

Top card title:

```text
Fluxo de Aprovação
```

Render a vertical three-step timeline with numbered blue markers.

Exact conceptual steps:

```text
1  Gestor aprova via Teams
   Aprovação do gestor da aplicação através do Microsoft Teams.

2  CAB agenda a janela
   CAB valida a mudança e agenda a janela de implantação.

3  Deploy PRD com lock sequencial
   Execução do deploy em PRD com controle de lock sequencial.
```

This is a summary/education component, not a second workflow engine. Azure DevOps remains the deployment approval authority per ADR-001.

## Right rail — Status

Second card title:

```text
Status
```

Initial state:

```text
Rascunho
A GMUD ainda não foi submetida para aprovação.
```

Use a small status indicator/icon and a visually clear status chip/badge.

Future statuses may include `Submitted`, `Manager Pending`, `CAB Pending`, `Scheduled`, `Approved`, `Rejected`, `Deploying`, and `Completed`, but do not implement the full lifecycle unless required by the current slice.

## Right rail — Identificador

Third card title:

```text
Identificador
```

Before creation:

```text
#
changeId será gerado após criar
```

After successful creation, replace this with the canonical `changeId`, for example:

```text
CHG-2026-004182
```

The `changeId` is the stable correlation identifier used across Backstage, the Change Management API, Azure DevOps, Teams, and any external ITSM provider.

## Bottom actions

Keep the main actions aligned to the bottom-right of the form/page on desktop.

Secondary:

```text
Salvar rascunho
```

Primary:

```text
Criar GMUD
```

The primary action should be visually stronger.

Do not add an `Approve` action on this screen. Creation and deployment approval are separate responsibilities.

## Visual fidelity rules for implementation agents

Agents implementing this screen MUST:

1. inspect `gmud-create-reference.jpg` before coding;
2. preserve the five numbered sections in the same order;
3. preserve the right-side approval/status/identifier rail on desktop;
4. preserve the labels shown in this specification unless a later ADR changes the domain language;
5. use Backstage/theme/MUI primitives rather than screenshot-specific absolute positioning;
6. keep the screen visually light, spacious, card-based, and enterprise-oriented;
7. keep `Criar GMUD` as the primary call-to-action;
8. not turn Teams, Backstage, SharePoint, Jira, or ServiceNow into a second Azure DevOps approval authority;
9. not couple the form contract to SharePoint-specific IDs or fields;
10. treat the reference values (`pagamentos-api`, `3.8.2`, `João Silva`, etc.) as examples, not hard-coded production data.

## Implementation preference

For the first implementation slice, favor a Backstage Scaffolder/form-based experience if it can reproduce this composition cleanly.

If standard Scaffolder rendering cannot achieve the visual contract without excessive hacks, it is acceptable to implement a thin GMUD frontend/plugin page earlier than originally planned, **provided it still calls the same provider-agnostic Change Management API and does not move workflow authority into Backstage**.

Visual fidelity is important, but architectural boundaries take precedence over mimicking the screenshot through brittle implementation techniques.
