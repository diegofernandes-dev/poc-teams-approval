# GMUD — Backstage implementation progress

> **Bridge repository:** `diegofernandes-dev/poc-teams-approval` — architectural handoff (this document)  
> **Implementation repository (ADO):** `platform-devops-developer-portal` — authoritative source code  
> **Checkpoint:** F2.0 — Change Management backend contract & architecture (complete) · F1.4 prior  
> **Prior checkpoints:** F1 (frontend shell) · F1.1 (visual polish) · F1.2 (Backstage-first composition) · F1.3 (semantic UX) · F1.4 (integrity cleanup)  
> **UI reference:** [`gmud-create-screen.md`](../ui/gmud-create-screen.md) · Backend contract: [ADR-006](../adr/ADR-006-change-management-backend-contract.md)  
> **Status:** F2.0 complete — **STOP** before F2.1 (architecture review gate)
>
> **Note:** ADO file paths in section 3 are **implementation references** in the Azure DevOps repository, not paths in this bridge repo.

---

## 1. Checkpoint summary

### F1 (functional shell)

F1 delivers a first-class frontend plugin for GMUD (Gestão de Mudanças) with:

- Route `/gmud` registered via the new Backstage frontend system
- Provider-agnostic `ChangeManagementApi` boundary with mock implementation
- Catalog- and identity-driven form context (component picker, requester, owner, evidence)
- Curated sidebar placement in the developer workflow section
- Automated tests for plugin wiring, API boundary, catalog context, and navigation

### F1.1 / F1.2 (visual composition)

Visual refinement only — no backend, persistence, or provider integration:

- Shared `useGmudCreateStyles` vocabulary (form surface, context fields, rail, actions)
- Controlled content width (max 1440) and ~76/24 main/rail columns
- Single form InfoCard surface with numbered sections (not four elevated cards)
- Backstage/MUI outlined controls; read-only context via `GmudContextField`
- Quiet right-rail cards (Fluxo, Status, Identificador)

### F1.3 (functional & semantic UX review)

F1.3 reframes the screen from **application deployment** to **generic production change management**. No backend, API expansion, or provider wiring.

#### Domain language changes

| Before (F1.2) | After (F1.3) | Rationale |
|---------------|--------------|-----------|
| Aplicação | **Alvo da mudança** | GMUD target is not limited to applications |
| Ambiente = PRD | **Removed** | Production is implicit on this screen |
| Versão / Artefato | **Removed** | Deployment-specific; not universal |
| Owner do sistema | **Responsável** | Clearer governance label; still `spec.owner` internally |
| Janela de Implantação | **Janela de Execução** | Generic execution window |
| Risco e Reversão (3/9 grid) | **Avaliação de Risco** (stacked) | Intentional hierarchy; reversal is a full-width field |
| Plano de rollback | **Plano de reversão** | Works for infra/DB/manual changes |
| Fluxo de Aprovação | **Fluxo da Mudança** | Not every post-creation step is an approval |
| Teams / CAB / Deploy PRD steps | Generic 3-step informational rail | No implementation leakage |

#### Fields added

- **Classificação da mudança** — `Normal` | `Emergencial` (required; default `normal`)

#### Right rail (informational only)

```text
Fluxo da Mudança
1  Aprovação do responsável
2  Validação da mudança
3  Autorização para execução
```

#### Canonical frontend model (F1.3)

```typescript
CreateChangeRequest {
  targetRef: string;                    // was componentRef
  classification: 'normal' | 'emergency';
  requestedBy: string;
  ownerRef?: string;
  systemRef?: string;                   // optional catalog enrichment — retained
  title: string;
  summary: string;
  requestedWindow: { date, startsAt, endsAt };
  risk: 'low' | 'medium' | 'high';
  rollbackPlan: string;
  evidence: ChangeEvidence[];
}
```

**Removed from model:** `environment`, `artifactVersion`, `componentRef`

#### Section 1 layout (F1.3)

```text
Row 1:  Alvo da mudança (md=8)  |  Classificação (md=4)
Row 2:  Título da mudança (full width)
Row 3:  Resumo (full width)
Row 4:  Solicitante              |  Responsável
```

#### Section 3 layout (F1.3 — post-review fix)

```text
Row 1:  Nível de risco (md=4)
Row 2:  Plano de reversão (full width, 4 rows)
```

Do **not** restore side-by-side risk/reversal (3/9) — it breaks form rhythm.

#### Evidence zero-state (neutral)

> Nenhuma evidência disponível no momento. Referências e documentos de apoio poderão ser associados conforme o contexto da mudança.

Catalog-derived evidence chips (ADO annotations, links) remain **contextual metadata**, not universal evidence definitions.

#### Remaining contextual elements (accepted)

- Catalog evidence may show `dev.azure.com/*` annotation values when present on the selected Component
- Page subtitle: *"Solicitação de mudança para produção"* — scope statement, not a form field

**Out of scope (F1.3):** backend APIs, persistence, ITSM providers, conditional GMUD types, workflow engine, System/Resource target selection, attachment storage.

---

## 2. Architecture decisions

| Decision | Rationale |
|----------|-----------|
| Dedicated plugin `@internal/plugin-change-management` | Keeps GMUD isolated from Scaffolder; enables clean API swap in F2 |
| `ChangeManagementApi` + `MockChangeManagementApi` | Frontend never imports provider-specific types; mock returns `MOCK-CHG-000001` |
| 4 form sections (no ITSM/provider section) | Aligns with reviewed F1 scope; provider wiring deferred |
| `targetRef` not `componentRef` (F1.3) | Domain language decoupled from Catalog Component kind; still Component-backed in F1.3 |
| `classification` not `environment` (F1.3) | Normal/Emergencial is governance classification, not environment |
| No `artifactVersion` in universal model (F1.3) | Artifact context is deployment-specific; may appear as evidence later |
| `systemRef` retained as optional (F1.3) | Useful catalog enrichment; not shown in UI |
| `rollbackPlan` property name unchanged (F1.3) | Internal API stability; UI label is "Plano de reversão" |
| Generic right-rail copy (F1.3) | Informational only; no Teams/ADO/Kubernetes/deployment assumptions |
| `validateGmudForm` extracted (F1.3) | Testable validation without DOM coupling |
| Plugin-local `makeStyles` only | Visual polish without global theme regressions |
| Feature discovery (`app.packages: all`) | Plugin registers via `src/alpha.tsx` |
| Sidebar via `nav.take('page:change-management')` | After Scaffolder in developer workflow group |

---

## 3. ADO file map

### Plugin (`plugins/change-management/`)

| Path | Purpose |
|------|---------|
| `package.json` | Frontend plugin package definition |
| `src/alpha.tsx` | `createFrontendPlugin` + `PageBlueprint` at `/gmud` |
| `src/routes.ts` | `rootRouteRef` |
| `src/index.ts` | Public exports incl. `ChangeClassification` |
| `src/api/ChangeManagementApi.ts` | API ref + `ApiBlueprint` extension |
| `src/api/MockChangeManagementApi.ts` | Mock `createChangeRequest` |
| `src/model/types.ts` | F1.3 `CreateChangeRequest` contract |
| `src/model/types.test.ts` | Model shape / removed-property architecture tests |
| `src/utils/catalogContext.ts` | Entity display name, evidence, owner/system refs |
| `src/utils/resolveRequesterContext.ts` | Identity + catalog requester resolution |
| `src/utils/gmudFormValidation.ts` | Form validation (classification, target, etc.) |
| `src/utils/gmudFormValidation.test.ts` | Validation unit tests |
| `src/components/GmudCreatePage/GmudCreatePage.tsx` | Page shell, layout, submit flow |
| `src/components/GmudCreatePage/GmudForm.tsx` | 4-section form (F1.3 semantics) |
| `src/components/GmudCreatePage/ApprovalFlowRail.tsx` | Right-rail Fluxo da Mudança / status / id |
| `src/components/GmudCreatePage/gmudCreateStyles.ts` | Shared visual vocabulary |
| `src/components/GmudCreatePage/GmudContextField.tsx` | Read-only context presentation |
| `src/**/*.test.ts(x)` | Unit and integration tests |

### Docs / UI reference

| Path | Purpose |
|------|---------|
| `docs/ui/gmud-create-reference.jpg` | Composition authority (labels superseded by F1.3 doc) |
| `docs/ui/gmud-create-screen.md` | **Normative F1.3 UI contract** |
| `docs/ui/screenshots/README.md` | F1.2 before / F1.3 after capture notes |

### App wiring

| Path | Change |
|------|--------|
| `packages/app/package.json` | Dependency on `@internal/plugin-change-management` |
| `app-config.yaml` | `page:change-management` path `/gmud` |
| `packages/app/src/modules/nav/Sidebar.tsx` | `nav.take('page:change-management')` |
| `packages/app/src/modules/nav/Sidebar.test.tsx` | Navigation test for GMUD link |

---

## 4. Tests and validation

### Commands run (F1.3 — 2026-08-30)

```bash
# Plugin unit/integration tests — PASS (22/22)
yarn test plugins/change-management --watchAll=false

# Plugin lint — PASS
yarn workspace @internal/plugin-change-management lint
```

### Test coverage

| Suite | Focus |
|-------|-------|
| `plugin.test.tsx` | Plugin registration, `/gmud` route |
| `GmudCreatePage.test.tsx` | Sections, domain purity, payload shape, classification, evidence zero-state |
| `model/types.test.ts` | Canonical model keys; no `environment`/`artifactVersion`/`componentRef` |
| `gmudFormValidation.test.ts` | Classification required; normal/emergency accepted |
| `catalogContext.test.ts` | Evidence, owner/system refs, display names |
| `resolveRequesterContext.test.ts` | Identity + catalog requester resolution |
| `Sidebar.test.tsx` | Navigation GMUD link |

### Payload boundary assertions (F1.3)

Submit payload must include: `targetRef`, `classification`, `requestedWindow`, `evidence`, `systemRef` (when catalog provides it).

Submit payload must **not** include: `environment`, `artifactVersion`, `componentRef`, or provider/workflow leakage (`teams`, `kubernetes`, `deploy`, etc.).

---

## 5. Visual validation (manual)

Compare `/gmud` against [`docs/ui/gmud-create-screen.md`](../ui/gmud-create-screen.md) (F1.3 contract supersedes deployment-centric labels in the JPG reference):

1. Start portal: `yarn start`
2. Sign in and open `/gmud` at ~1440px width
3. Verify F1.3 semantics:
   - **Alvo da mudança** + **Classificação** (no Ambiente/PRD, no Versão/Artefato)
   - **Janela de Execução** (not Implantação)
   - **Avaliação de Risco** — stacked risk + full-width reversal plan
   - **Fluxo da Mudança** rail (no Teams/CAB/deploy wording)
   - Neutral evidence zero-state when catalog has no links/annotations
4. Submit — expect success alert with mock ID `MOCK-CHG-000001`
5. Capture `docs/ui/screenshots/gmud-create-f1.3-after.png` (manual; auth required)

### F1.3 vs F1.2 comparison

| Element | F1.2 | F1.3 |
|---------|------|------|
| Section 1 row 1 | Aplicação / Ambiente PRD / Versão·Artefato | Alvo da mudança / Classificação |
| Section 2 title | Janela de Implantação | Janela de Execução |
| Section 3 | Risco e Rollback (3/9 side-by-side) | Avaliação de Risco (stacked) |
| Owner label | Owner do sistema | Responsável |
| Right rail | Fluxo de Aprovação (Teams/CAB/deploy) | Fluxo da Mudança (generic) |
| Model | `componentRef`, `environment: PRD`, `artifactVersion?` | `targetRef`, `classification` |

---

## 6. Field justification (F1.3)

Every visible element must answer: WHAT changes · WHEN · RISK/reversal · EVIDENCE · or WHAT HAPPENS AFTER creation.

| Field / element | Purpose |
|-----------------|---------|
| Alvo da mudança | WHAT — scope of the change |
| Classificação | Governance classification (normal vs emergency) |
| Título / Resumo | Describe the change |
| Solicitante | Auditability (identity-derived) |
| Responsável | Governance ownership (catalog `spec.owner`) |
| Janela de Execução | WHEN — authorized execution window |
| Nível de risco | Risk evaluation |
| Plano de reversão | Recovery if change fails |
| Evidências | Supporting references (catalog-derived today) |
| Fluxo da Mudança | Post-creation process (informational) |
| Status | Current change state |
| Identificador | Stable correlation ID (`changeId`) |

---

## 7. Next slice — gate before F2

**F1.3 stop condition:** review domain model and screen with architecture stakeholders before proceeding.

F2 (when approved) should:

1. Implement backend `ChangeManagementService` behind `ChangeManagementApi`
2. Replace `MockChangeManagementApi` factory with real client
3. Accept F1.3 `CreateChangeRequest` shape — do not reintroduce `environment`/`artifactVersion` without ADR
4. Add provider configuration (app-config + secrets) without leaking provider fields into frontend types
5. Wire RBAC permissions for create/read change requests
6. Add ADR for ITSM integration approach

**F1.3 handoff complete.** Frontend domain model is generic change-management; backend swap is isolated to `changeManagementApiExtension` factory only.

---

## 8. GMUD F1.4 — Architecture & Functional Integrity Cleanup

**Checkpoint:** F1.4 — corrective alignment before any backend work. **STOP** — do not begin F2.

### Corrections applied

| Area | F1.3 state | F1.4 correction |
|---|---|---|
| ADR set on `main` | ADR-002 only | ADR-001 through ADR-005 consolidated; MVP branch superseded |
| ADR-002 provider wording | SharePoint implied as GMUD record of truth | References ADR-003; SharePoint optional adapter only |
| UI contract Section 5 | ITSM/provider section present | Removed — provider choice not in developer UX |
| Right rail step 1 | Aprovação do responsável | **Aprovação do gestor** (Catalog Responsável remains ownership context) |
| Classification | Default `normal` | Initial `''`; explicit selection required |
| Draft action | Fake “Salvar rascunho” alert | Removed — no persistence in F1.4 |
| Evidence semantics | Catalog metadata shown/sent as `evidence[]` | Target context in Section 1; Section 4 zero-state; `evidence: []` on submit |

### Evidence semantic decision (F1.4)

**Option A — relabel and separate:** catalog-derived links display under Section 1 **Referências de contexto** with explicit copy that they are target context, not change evidence. Section 4 **Evidências** shows zero-state only. Submit payload sends `evidence: []` until real change-specific evidence workflows exist.

### Bridge files changed

| Path | Change |
|---|---|
| `docs/adr/README.md` | New — index + MVP branch supersession note |
| `docs/adr/ADR-001-azure-devops-approval-authority.md` | Promoted from MVP branch |
| `docs/adr/ADR-002-backstage-change-onramp.md` | Rewritten — provider-neutral; references ADR-003 |
| `docs/adr/ADR-003-provider-agnostic-change-management.md` | Promoted from MVP branch |
| `docs/adr/ADR-004-teams-delegated-approval-identity.md` | Promoted from MVP branch |
| `docs/adr/ADR-005-cab-scheduling-and-concurrency.md` | Promoted from MVP branch |
| `docs/ui/gmud-create-screen.md` | F1.4 contract — four sections, gestor copy, classification, draft removal, evidence semantics |
| `docs/backstage/current-state.md` | F2.0 snapshot; backend scaffold; STOP before F2.1 |
| `docs/future-gmud-context-enrichment.md` | Supersession banner pointing to ADR-003 |
| `docs/backstage/implementation-progress.md` | This section |

### ADO files changed

| Path | Change |
|---|---|
| `plugins/change-management/src/components/GmudCreatePage/GmudCreatePage.tsx` | Empty classification initial; `evidence: []`; remove draft handler |
| `plugins/change-management/src/components/GmudCreatePage/GmudForm.tsx` | Classification placeholder; target context in Section 1; remove draft button |
| `plugins/change-management/src/components/GmudCreatePage/ApprovalFlowRail.tsx` | Gestor approval copy |
| `plugins/change-management/src/utils/catalogContext.ts` | `buildTargetContextFromComponent` rename |
| `plugins/change-management/src/utils/catalogContext.test.ts` | Updated for rename |
| `plugins/change-management/src/components/GmudCreatePage/GmudCreatePage.test.tsx` | F1.4 assertions |

### Commits

| Repository | Branch | SHA |
|---|---|---|
| ADO `platform-devops-developer-portal` | `feat/ado-repo-governance` | `52e01ca` |
| Bridge `poc-teams-approval` | `main` | `cd0c799` |

### Tests executed (ADO — 2026-08-30)

```bash
yarn workspace @internal/plugin-change-management test   # PASS 24/24
yarn workspace @internal/plugin-change-management lint     # PASS
yarn tsc                                                   # PASS
yarn tsc:full                                              # FAIL — pre-existing node_modules type errors (unrelated to F1.4)
yarn lint                                                  # FAIL — pre-existing packages/app undeclared @backstage/plugin-catalog-react import (unrelated to F1.4)
```

### Deviations

None between ADO implementation and F1.4 normative contract at time of handoff.

### Unresolved questions

1. **Approver resolution** — how managerial approver is derived from identity/org structure (deferred; UI copy only in F1.4).
2. **ADR-004 / ADR-005** — remain Proposed pending technical validation.
3. **Draft persistence** — deferred until backend `ChangeManagementService` exists.

### Next recommended slice

Architecture review gate on consolidated ADR set + F1.4 contract. **Do not start F2** until approved.

---

## 9. GMUD F2.0 — Change Management Backend Contract & Architecture

**Checkpoint:** F2.0 — backend contract and architecture scaffold. **STOP** — do not begin F2.1.

### Objective

Define backend architecture for create/get change capabilities without implementing real ITSM providers, Azure DevOps integration, or frontend wiring.

### Architecture decisions (summary)

| Decision | Choice |
|---|---|
| Service boundary | `ChangeManagementService` → `IChangeManagementProvider` |
| Trusted HTTP create fields | targetRef, classification, title, summary, requestedWindow, risk, rollbackPlan, evidence |
| Server-derived fields | requestedBy (identity), ownerRef/systemRef (catalog), changeId, status, timestamps |
| changeId ownership | Service-generated before provider write; discarded on provider failure |
| Minimal status | `submitted` only (no `draft`) |
| Provider metadata | Internal `ProviderReference` — not in public `Change` |
| Authorization | `change-management.change.create` / `.read` via Permission Framework |
| Idempotency | Optional `Idempotency-Key` header |
| Timezone | `changeManagement.defaultTimezone` → UTC storage |

See [ADR-006](./../adr/ADR-006-change-management-backend-contract.md) for full contract.

### Pre-F2 semantic hygiene

| File | Change |
|---|---|
| `docs/adr/ADR-003-provider-agnostic-change-management.md` | `targetRef`, requested execution window, optional ADO correlation |
| `docs/adr/README.md` | Universal flow ends at controlled execution; ADO/CD as optional path |

### Bridge files changed

| Path | Change |
|---|---|
| `docs/adr/ADR-003-provider-agnostic-change-management.md` | Semantic correction |
| `docs/adr/README.md` | Platform flow + ADR-006 index |
| `docs/adr/ADR-006-change-management-backend-contract.md` | New — F2 backend contract |
| `docs/backstage/current-state.md` | F2.0 snapshot — backend scaffold, STOP before F2.1 |
| `docs/backstage/implementation-progress.md` | This section |

### ADO files changed

| Path | Purpose |
|---|---|
| `packages/backend/src/plugins/changeManagementPlugin.ts` | HTTP routes POST/GET |
| `packages/backend/src/modules/changeManagement/types.ts` | Domain + transport types |
| `packages/backend/src/modules/changeManagement/permissions.ts` | Permission definitions |
| `packages/backend/src/modules/changeManagement/validation.ts` | Zod schemas |
| `packages/backend/src/modules/changeManagement/windowNormalizer.ts` | Timezone → UTC |
| `packages/backend/src/modules/changeManagement/targetContextResolver.ts` | Catalog owner/system |
| `packages/backend/src/modules/changeManagement/changeIdGenerator.ts` | Service-owned ID format |
| `packages/backend/src/modules/changeManagement/errors.ts` | Stable error codes |
| `packages/backend/src/modules/changeManagement/idempotency.ts` | Key handling |
| `packages/backend/src/modules/changeManagement/IChangeManagementProvider.ts` | Provider interface |
| `packages/backend/src/modules/changeManagement/FakeChangeManagementProvider.ts` | In-memory test provider |
| `packages/backend/src/modules/changeManagement/ChangeManagementService.ts` | create/get orchestration |
| `packages/backend/src/modules/changeManagement/*.test.ts` | Service, validation, architecture tests |
| `packages/backend/src/index.ts` | Register plugin |
| `packages/backend/config/rbac/rbac-policy.csv` | change-management permissions |
| `packages/backend/src/modules/templateExecutorRoleSeed.ts` | template_executor GMUD permissions |
| `app-config.yaml` | changeManagement config + pluginsWithPermission |

### Commits

| Repository | Branch | SHA |
|---|---|---|
| ADO `platform-devops-developer-portal` | `feat/ado-repo-governance` | `b2bed17` |
| Bridge `poc-teams-approval` | `main` | `4ec7292` (F2.0 contract) · `317b821` (architect snapshot refresh) |

### Tests executed (ADO)

```bash
yarn workspace backend test --watchAll=false   # PASS 61/61 (16 suites)
yarn workspace backend lint                    # PASS
yarn tsc                                       # PASS
```

### Deviations

None between ADO scaffold and ADR-006 at time of handoff.

### Unresolved questions

1. **Multi-timezone** — single org default vs per-system timezone (deferred).
2. **GET read scope expansion** — team-wide listing deferred to F3.
3. **Evidence workflow** — F2.0 accepts `evidence: []` only on create.
4. **Component-only targetRef** — System/Resource targets need future ADR.

### Next recommended slice (F2.1)

1. Development provider with durable persistence
2. Real changeId sequence
3. Wire frontend `ChangeManagementApi` to backend
4. Remove trusted fields from frontend POST payload
5. Persistent idempotency
6. **STOP** before SharePoint/Jira/ServiceNow

**F2.0 handoff complete.** Wait for architecture review before F2.1.

---

## 10. GMUD F2.0 Architecture Review — Canonical Record Ownership

**Checkpoint:** F2.0 architecture review — record authority decision before F2.1 durable persistence. **STOP** — do not begin F2.1 ADO implementation until stakeholder acceptance.

### Objective

Resolve who owns the canonical GMUD record before the F2.1 persistence decision becomes irreversible. Evaluate provider-owned, platform-owned, and hybrid models. Document deviations between ADO `b2bed17` and ADR-006. Produce ADR-007 and ADR clarifications.

### Recommended ownership model

**Model C — Hybrid Canonical Index + Provider Record** ([ADR-007](../adr/ADR-007-change-record-authority.md))

| Authority | Owner |
|---|---|
| Domain schema / API | Platform |
| Canonical `changeId` / idempotency | Platform |
| Platform canonical index (F2.1+) | Platform — identity, routing, audit snapshot |
| Operational GMUD record (production) | ITSM provider |
| Operational GMUD record (F2.1 dev) | `DevelopmentProvider` — non-production |

**Rejected:**

- **Model A (provider-owned only):** Cannot route historical reads after provider config change without a platform index; F2.0 code is incomplete Model A
- **Model B (platform-owned store):** Violates ADR-002; risk of building ServiceNow inside Backstage

### Provider switch behavior (explicit)

Multi-provider coexistence indefinitely:

- `GET CHG-2026-000100` (SharePoint era) → platform index → `providerKey: sharepoint` → SharePoint adapter
- `GET CHG-2027-000001` (Jira era) → platform index → `providerKey: jira` → Jira adapter
- `POST /changes` (2027) → new records use configured default provider
- Immutable `providerKey` per change; bulk migration optional operational project

### ADO `b2bed17` vs ADR-006 — deviations and gaps

| Topic | ADR-006 (pre-review) | ADO `b2bed17` | Resolution |
|---|---|---|---|
| `changeId` sequence location | Stated as fake provider | `changeIdGenerator.ts` in service | ADR-006 corrected — code correct |
| Idempotency location | Stated as fake provider | `idempotency.ts` service store | ADR-006 corrected — code correct |
| `ProviderReference` persistence | Provider returns reference | Service discards result | F2.1 gap — persist in platform index |
| Record authority | Ambiguous | Provider is de facto GET source | ADR-007 clarifies Model C |
| GET routing | Not specified | Direct `provider.get(changeId)` | F2.1 gap — index → adapter |
| Idempotency race / retry | Not addressed | Race possible; retry after provider fail gets new ID | F2.1 gap — DB constraint + early reserve |

**Confirmed aligned:** service-owned `changeId`; provider does not mint IDs; fail-closed 503; server-derived `requestedBy`/`ownerRef`/`systemRef`; forbidden fields absent from canonical types.

### F2.1 readiness

| Gate | Status |
|---|---|
| Ownership model decided | Yes — Model C |
| ADR-007 + ADR-003/006 updates | Complete (this commit) |
| Stakeholder acceptance | **Accepted 2026-08-31** |
| F2.1 ADO coding | **Conditional GO** — after ADO realignment to `b2bed17` (see §11) |

### Bridge files changed

| Path | Change |
|---|---|
| `docs/adr/ADR-007-change-record-authority.md` | **New** — Model C decision, authority table, provider-switch rules |
| `docs/adr/ADR-006-change-management-backend-contract.md` | Idempotency/changeId location fix; snapshots; GET routing; ADR-007 reference |
| `docs/adr/ADR-003-provider-agnostic-change-management.md` | Narrowed replaceability guarantee; platform index |
| `docs/adr/README.md` | ADR-007 index entry |
| `docs/backstage/current-state.md` | Architecture review outcome; conditional GO gate |
| `docs/backstage/implementation-progress.md` | This section |

### ADO files changed

None — architecture review is documentation-only. F2.1 ADO changes deferred until acceptance:

1. `ChangeRepository` / platform canonical index
2. Persist `ProviderReference`; GET via index → provider
3. Durable idempotency + `changeId` sequence
4. `DevelopmentProvider` (non-production)
5. Frontend wiring

### Commits

| Repository | Branch | SHA |
|---|---|---|
| ADO `platform-devops-developer-portal` | `feat/ado-repo-governance` | `b2bed17` (unchanged) |
| Bridge `poc-teams-approval` | `main` | **`57613ab`** (F2.0 architecture review) |

### Tests executed

No ADO code changes — no tests run for this checkpoint.

### Deviations

§9 stated "None between ADO scaffold and ADR-006 at time of handoff." This review identifies:

- **Documentation errors** in ADR-006 (idempotency/sequence location) — corrected
- **Architecture gaps** in F2.0 code vs target Model C — documented for F2.1; not silent rewrites

### Unresolved questions

1. Stakeholder acceptance of ADR-007 — required before F2.1
2. Multi-timezone — deferred
3. GET read scope expansion — F3
4. Evidence workflow — F2.0 accepts `[]` only

### Next recommended slice (F2.1 — after acceptance)

Per ADR-007 conditional GO:

1. Platform canonical index + durable idempotency + `changeId` sequence
2. `DevelopmentProvider` (non-production)
3. Wire frontend to backend
4. **STOP** before SharePoint/Jira/ServiceNow

**Architecture review complete.** ADR-007 accepted 2026-08-31. See §11 for ADO realignment.

---

## 11. ADO uncommitted Model B drift — detected and reverted

**Checkpoint:** Independent architecture review (F2.0 record-authority gate). Detected
uncommitted ADO working-tree changes that implemented **Model B** (platform-owned
full record) by removing the provider layer. **Reverted** to F2.0 commit `b2bed17`
before any approved F2.1 work.

### Deviation summary

| Aspect | ADO `b2bed17` (F2.0) | Uncommitted working tree (reverted) |
|---|---|---|
| Provider layer | `IChangeManagementProvider` + `FakeChangeManagementProvider` | **Removed** |
| Persistence | In-memory fake provider | `KnexChangeRepository` + `migrations/changeManagement/001_initial.cjs` |
| Record authority | Incomplete Model A (provider de facto GET source) | **Model B** — platform stores complete `Change` |
| Routing metadata | N/A (F2.1 gap) | **Absent** — no `providerKey` / `externalId` |
| Error code | `PROVIDER_UNAVAILABLE` | `PERSISTENCE_UNAVAILABLE` |

### ADO files affected (reverted)

| Path | Action |
|---|---|
| `packages/backend/src/modules/changeManagement/ChangeManagementService.ts` | Restored to `b2bed17` |
| `packages/backend/src/modules/changeManagement/ChangeManagementService.test.ts` | Restored to `b2bed17` |
| `packages/backend/src/modules/changeManagement/IChangeManagementProvider.ts` | Restored |
| `packages/backend/src/modules/changeManagement/FakeChangeManagementProvider.ts` | Restored |
| `packages/backend/src/modules/changeManagement/changeIdGenerator.ts` | Restored |
| `packages/backend/src/modules/changeManagement/idempotency.ts` | Restored |
| `packages/backend/src/modules/changeManagement/architecture.test.ts` | Restored |
| `packages/backend/src/modules/changeManagement/errors.ts` | Restored |
| `packages/backend/src/modules/changeManagement/types.ts` | Restored |
| `packages/backend/src/plugins/changeManagementPlugin.ts` | Restored |
| `packages/backend/package.json` | Restored (`migrations` files entry removed) |
| `packages/backend/src/modules/changeManagement/ChangeRepository.ts` | **Deleted** (untracked) |
| `packages/backend/src/modules/changeManagement/ChangeRepository.test.ts` | **Deleted** (untracked) |
| `packages/backend/migrations/changeManagement/001_initial.cjs` | **Deleted** (untracked) |

### Verification (ADO after realignment)

```bash
yarn workspace backend test --watchAll=false   # PASS 61/61 (16 suites)
```

### Resolution

The drift violated ADR-007 Model C and ADR-002 ("Backstage does not become the GMUD
record database"). F2.1 must implement the **platform canonical index +
`IChangeManagementProvider`** target per ADR-007 — not a monolithic repository that
replaces the provider abstraction. See ADR-007 anti-pattern section.

### Gate

| Gate | Status |
|---|---|
| ADR-007 stakeholder acceptance | **Accepted 2026-08-31** |
| ADO code realigned to `b2bed17` | **Yes** |
| F2.1 ADO coding | **Conditional GO** per ADR-007 §F2.1 scope |

### Bridge files changed

| Path | Change |
|---|---|
| `docs/adr/ADR-007-change-record-authority.md` | Stakeholder acceptance; anti-pattern note |
| `docs/backstage/current-state.md` | Acceptance gate; realignment note |
| `docs/backstage/implementation-progress.md` | This section |

### ADO files changed

Realignment only — restored F2.0 scaffold at `b2bed17`; no F2.1 implementation.

### Next recommended slice (F2.1)

Per ADR-007 conditional GO:

1. Platform canonical index (`ChangeRepository`) with routing metadata — **not** Model B monolith
2. Restore/retain `IChangeManagementProvider` + `DevelopmentProvider` (non-production)
3. Durable idempotency + `changeId` sequence
4. Wire frontend to backend (separate checkpoint — stop for review after backend persistence)
5. **STOP** before SharePoint/Jira/ServiceNow

---

## 12. F2.1 backend persistence checkpoint (Model C)

**Checkpoint:** F2.1 durable canonical index + `DevelopmentProvider` (backend only; no frontend wiring).

### ADO implementation summary

| Component | Implementation |
|---|---|
| `ChangeIndexRepository` | `KnexChangeIndexRepository` → `change_index` |
| `IdempotencyRepository` | `KnexIdempotencyRepository` → `change_idempotency` |
| `ChangeIdGenerator` | `DatabaseChangeIdGenerator` → `change_id_sequences` |
| `ProviderRegistry` | `DefaultProviderRegistry` — default from `changeManagement.provider` |
| `DevelopmentProvider` | `development_change_records` — non-production operational store |
| HTTP routes | Unchanged POST/GET contract |
| Status | `submitted` only |

### Migration

| File | Tables |
|---|---|
| `packages/backend/migrations/change-management/20260831120000_initial.cjs` | `change_index`, `change_idempotency`, `change_id_sequences`, `development_change_records` |

### Tests executed (ADO)

```bash
yarn workspace backend lint                    # PASS
yarn workspace backend test --watchAll=false   # PASS 71/71 (17 suites)
```

Coverage includes: canonical index persistence, immutable `providerKey`, GET routing via stored key, durable idempotency (same-key/conflict/concurrent), platform-owned `changeId`, provider failure 503, auth on historical snapshots, architecture guards (no Model B), degraded read returns 503.

### Deviations

| Item | Notes |
|---|---|
| Frontend wiring | **Intentionally deferred** per F2.1 backend-only checkpoint scope |
| Degraded read | Returns **503** when provider unavailable; explicit `meta.source` deferred to future slice |
| Idempotency FK | No FK from `change_idempotency.change_id` → `change_index` (allows early reserve before index insert) |
| Orphan local SQLite | Prior Model B-shaped `changes` table in dev data discarded; fresh Model C migration used |

### Failure / recovery (documented)

| Scenario | Behavior |
|---|---|
| Provider fails before finalize | 503 to client; pending index row not visible on GET; idempotency `pending` |
| Provider succeeds, index finalize fails | Log `change.create.orphan`; 503 to client; idempotent retry via same `Idempotency-Key` |
| `DevelopmentProvider` (same DB) | `provider.create` + index finalize + idempotency complete in single Knex transaction |

Production ITSM adapters must implement idempotent `create` by platform `changeId` for safe retry.

### Architecture review answers

1. `ChangeManagementService` depends on `IChangeManagementProvider` via `ProviderRegistry` — **Yes**
2. `providerKey` immutable and persisted — **Yes** (`change_index.provider_key`)
3. Historical records route to original provider — **Yes** (GET uses stored `providerKey`)
4. `changeId` platform-owned — **Yes** (`DatabaseChangeIdGenerator`)
5. Idempotency platform-owned — **Yes** (`IdempotencyRepository`)
6. `ownerRef`/`systemRef` historical snapshots — **Yes** (no Catalog re-fetch on GET)
7. Canonical index distinct from provider persistence — **Yes** (separate tables + interface)
8. Model B accidentally implemented — **No** (`IChangeManagementProvider` active; no monolithic `ChangeRepository`)
9. Provider succeeds, index finalize fails — Orphan logged; 503; retry runbook documented above
10. Ready for frontend wiring — **Conditional GO** after architecture review

### Gate

| Gate | Status |
|---|---|
| F2.1 backend persistence | **Complete** (awaiting review) |
| Frontend wiring | **STOP** until review |
| SharePoint/Jira/ServiceNow | **STOP** |

### ADO commit

**`0dc3ed4`** on branch `feat/ado-repo-governance` (baseline F2.0: `b2bed17`).

```text
feat(gmud): F2.1 durable canonical index and DevelopmentProvider
```

### Bridge commit

**`196949c`** — records ADO F2.1 commit `0dc3ed4` (follow-up to initial handoff `c1691f9`).

---

## 13. F2.1.1 idempotency recovery checkpoint

**Checkpoint:** Crash-safe idempotency retry semantics (backend only; no frontend wiring).

**Baseline:** ADO F2.1 commit `0dc3ed4`.

### Idempotency state machine

| State | Meaning |
|---|---|
| `pending` | Request reserved; `changeId` may be assigned; finalize not yet complete |
| `completed` | Index finalized and idempotency closed — same key+payload returns cached result |

Stored per record: `idempotencyKey`, `payloadHash`, `changeId`, `state`, `status`, `createdAt`.

### Retry semantics

| Idempotency | Index | Retry action |
|---|---|---|
| `completed` | any | Return cached `{ changeId, status }` |
| `pending` | finalized | Heal: `complete()` idempotency, return result (Case 3) |
| `pending` | unfinalized + `changeId` | Resume `finalizeCreate()` with same `changeId` |
| `pending` | no index + `changeId` | `insertPending()` then `finalizeCreate()` (Case 1) |
| any | any | Same key + different payload → **409** |

### Provider idempotency guarantee

`IChangeManagementProvider.create(change)` must be idempotent by canonical `change.changeId`. Proven in `DevelopmentProvider` (upsert) and `FakeChangeManagementProvider` (test adapter).

### Crash cases covered (tests)

| Case | Scenario | Verified |
|---|---|---|
| 1 | `changeId` claimed, crash before provider | Resume with same `changeId` |
| 2 | Provider OK, finalize fails (orphan) | Retry reconciles; orphan log with `idempotencyKey` |
| 3 | Index finalized, idempotency pending | Heal to `completed` without provider call |
| 4 | Provider fails | Retry same `changeId` after provider available |
| — | Concurrent same-key | Single `changeId`, single provider record, single finalized index |
| — | Failed retry | No second sequence allocation |
| — | Pending payload conflict | **409** |

### ADO files changed

| Path | Change |
|---|---|
| `ChangeManagementService.ts` | Recovery helpers (`tryResolveIdempotentCreate`, `healCompletedIdempotency`); `change.create.recover` log; orphan log includes `idempotencyKey` |
| `IChangeManagementProvider.ts` | Idempotent create contract (JSDoc) |
| `FakeChangeManagementProvider.ts` | Upsert-by-`changeId`; create call counter for tests |
| `KnexIdempotencyRepository.ts` | Idempotent `complete()` (`WHERE state = pending`) |
| `testHelpers.ts` | Seed helpers, `FaultInjectingChangeIndexRepository` |
| `ChangeManagementService.recovery.test.ts` | Fault-injection recovery tests (new) |
| `DevelopmentProvider.test.ts` | Provider idempotency proof (new) |
| `ChangeManagementService.integration.test.ts` | Concurrent provider/index dedup assertion |

### Tests executed (ADO)

```bash
yarn workspace backend lint                    # PASS
yarn workspace backend test --watchAll=false   # PASS 80/80 (19 suites)
```

### Architecture review answers (F2.1.1)

1. Durable idempotency states — `pending`, `completed` only
2. `changeId` assigned once per key — **Yes** (`claimChangeId`)
3. Pending resumed — synchronous retry with stored `changeId`
4. Provider.create already succeeded — idempotent create + finalize + complete
5. Safe to call provider.create twice — **Yes** (required contract)
6. Finalized index + pending idempotency — heal to `completed`, no provider call
7. Concurrent same-key duplicates — **No** (DB PK + conditional claim + tests)
8. Recovery cannot complete — **503**, never new `changeId` for same key
9. Background reconciler required — **No**
10. Frontend wiring safe after checkpoint — **Conditional GO** after architecture review

### Deviations

| Item | Notes |
|---|---|
| Internal `changeId` allocated before client-visible success | Unchanged from F2.1 — client still gets 503 until finalize |
| No idempotency FK to index | Unchanged — intentional for early reserve |
| ADO commit SHA | **`ed6810b`** on `feat/ado-repo-governance` (baseline F2.1: `0dc3ed4`) |

### Unresolved reliability risks

- External ITSM adapters must implement idempotent create by platform `changeId` before production use
- Lost `claimChangeId` race may consume an extra sequence number (uniqueness preserved)
- No TTL/sweeper for abandoned pending requests without retries (acceptable — retry-driven recovery)

### Gate

| Gate | Status |
|---|---|
| F2.1.1 idempotency recovery | **Complete** (awaiting review) |
| Frontend wiring | **STOP** until F2.1.1 review |
| SharePoint/Jira/ServiceNow | **STOP** |

### ADO commit

**`ed6810b`** on branch `feat/ado-repo-governance` (baseline F2.1: `0dc3ed4`).

```text
feat(gmud): F2.1.1 idempotency recovery and crash-safe retry semantics
```

### Bridge commit

**`a65e1ed`** · **`44729aa`** · **`afaaaf6`** on branch `main` (ADO SHA `ed6810b`).

---

## 14. F2.1.2 — Multi-activity execution plan (architecture + backend)

### Checkpoint summary

F2.1.2 extends the canonical GMUD domain with **`ExecutionPlan`** / **`ExecutionActivity`** per [ADR-008](../adr/ADR-008-multi-activity-change-execution-plan.md). A single GMUD may describe multiple planned execution activities with different responsible teams and optional activity-specific targets.

**Backend only** — frontend wiring and Plano de execução UI remain deferred to F2.1.3.

### Architecture decisions (ADR-008)

| Topic | Decision |
|---|---|
| Cardinality | `executionPlan.activities` required, min 1, max 20 |
| `Change.targetRef` | Retained as primary catalog anchor |
| `ownerRef` vs `responsibleRef` | Owner = governance (server-derived); responsible = activity team (client Group) |
| Activity `targetRef` | Optional Component |
| Ordering | Array order authoritative; no `sequence` / `dependsOn` |
| Window / rollback | Change-level only |
| Activity status | **Rejected** — no workflow states |
| Authorization | `responsibleRef` does **not** grant read access (F3) |
| Index snapshot | Full immutable `executionPlan` with server `activityId`s |

### ADO files changed

| Path | Change |
|---|---|
| `types.ts` | `ExecutionPlan`, `ExecutionActivity`, `ExecutionActivityInput` types on `Change` / `CreateChangeHttpRequest` |
| `validation.ts` | Zod schema for `executionPlan` (min 1, max 20 activities) |
| `executionPlanValidator.ts` | Catalog validation for Group `responsibleRef`, optional Component `targetRef` |
| `ChangeManagementService.ts` | Validate plan; assign `activityId` (UUID) at create |
| `persistence/changeIndexMapper.ts` | `execution_plan_json` snapshot column mapping |
| `migrations/change-management/20260901120000_add_execution_plan_to_change_index.cjs` | Index migration |
| `testHelpers.ts` | Default `executionPlan` in `validRequest` |
| `plugins/change-management/src/model/types.ts` | Shared GET model + optional POST `executionPlan` until F2.1.3 UI |
| `executionPlanValidator.test.ts` | Validator unit tests (new) |
| `ChangeManagementService.test.ts` | Persistence + idempotency conflict tests |

### Tests executed (ADO)

```bash
yarn workspace backend lint                    # PASS
yarn workspace backend test --watchAll=false   # PASS 88/88 (20 suites)
```

### Gate

| Gate | Status |
|---|---|
| F2.1.2 architecture | **Accepted** — ADR-008 |
| F2.1.2 backend | **Complete** (awaiting ADO commit SHA) |
| Frontend wiring | **STOP** until F2.1.3 |
| SharePoint/Jira/ServiceNow | **STOP** |

### Bridge commit

**`afb154b`** on branch `main` (ADR-008 + doc updates).

### ADO commit

Pending commit on branch `feat/ado-repo-governance` (baseline F2.1.1: `ed6810b`).

```text
feat(gmud): F2.1.2 multi-activity execution plan domain
```
