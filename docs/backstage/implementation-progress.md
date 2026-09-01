# GMUD — Backstage implementation progress

> **Bridge repository:** `diegofernandes-dev/poc-teams-approval` — architectural handoff (this document)  
> **Implementation repository (ADO):** `platform-devops-developer-portal` — authoritative source code  
> **Checkpoint:** F3.0.1 — Authorization Architecture Convergence (accepted; documentation only) · F2.2.1 implementation baseline
> **Prior checkpoints:** F1 (frontend shell) · F1.1 (visual polish) · F1.2 (Backstage-first composition) · F1.3 (semantic UX) · F1.4 (integrity cleanup) · F2.0 (backend contract & architecture) · F2.1 (durable index + DevelopmentProvider) · F2.1.1 (idempotency recovery) · F2.1.2 (execution plan domain, ADR-008) · F2.1.3 (frontend wiring)  
> **UI reference:** [`gmud-create-screen.md`](../ui/gmud-create-screen.md) · [`gmud-my-changes-screen.md`](../ui/gmud-my-changes-screen.md) · [`gmud-detail-screen.md`](../ui/gmud-detail-screen.md) · Backend contract: [ADR-006](../adr/ADR-006-change-management-backend-contract.md)  
> **Status:** ADR-009 accepted — **GO** for F3.1 implementation planning; **NO-GO** for implementation until a reviewed plan authorizes it
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
| F2.1.2 backend | **Complete** |
| Frontend wiring | **STOP** until F2.1.3 |
| SharePoint/Jira/ServiceNow | **STOP** |

### Architecture review answers (F2.1.2)

1. `executionPlan` required — **Yes** (min 1 activity)
2. `Change.targetRef` retained — **Yes** (primary catalog anchor)
3. `responsibleRef` = Catalog Group only — **Yes**
4. Activity `targetRef` optional Component — **Yes**
5. Per-activity status/workflow — **Rejected**
6. `responsibleRef` grants read — **No** (deferred F3)
7. Index snapshot includes full plan — **Yes**
8. Idempotency includes plan in hash — **Yes**
9. Frontend UI — **STOP** until F2.1.3

### Deviations

None — ADO implementation matches ADR-008.

### Bridge commit

**`afb154b`** · **`75b4f38`** on branch `main` (ADR-008 + handoff).

### ADO commit

**`5e4f30e`** on branch `feat/ado-repo-governance` (baseline F2.1.1: `ed6810b`).

```text
feat(gmud): F2.1.2 multi-activity execution plan domain
```

---

## 15. F2.1.3 — Frontend execution plan + real backend wiring

### Checkpoint summary

The `/gmud` route now completes the first real creation path:

```text
GmudCreatePage → ChangeManagementApi → ChangeManagementClient
→ POST /api/change-management/changes → ChangeManagementService
→ Model C persistence + DevelopmentProvider
```

The form has five numbered sections and starts with exactly one empty execution
activity. It supports 1–20 ordered activities, requires a Catalog Group as
`responsibleRef`, accepts an optional Catalog Component as activity `targetRef`, and
does not expose provider, workflow, approval, status, pipeline, or ADO concerns.

Success uses the POST result and a submitted-form snapshot to show the canonical
`changeId`. No create-time GET, detail route, provider metadata, or automatic HTTP retry
was introduced.

### ADO files changed

| Area | Files / result |
|---|---|
| API abstraction and wiring | `ChangeManagementApi.ts`, `ChangeManagementClient.ts`, `changeManagementErrors.ts`; discovery/fetch real client is the default; mock remains test/fixture-only |
| Form and success UX | `GmudCreatePage.tsx`, `GmudForm.tsx`, `GmudCreatedSummary.tsx`, `ApprovalFlowRail.tsx`, `gmudCreateStyles.ts` |
| Trusted transport and retry helpers | `buildCreateHttpBody.ts`, `gmudFormValidation.ts`, `idempotencyKey.ts`, `mapChangeManagementError.ts` |
| Public frontend types | `model/types.ts`, `index.ts`; `CreateChangeHttpBody.executionPlan` is required and create is the only API operation in this slice |
| Frontend tests/config | API client, form, submit, payload, validation, error, model, and plugin wiring tests; plugin Jest worker cap |
| Backend proof only | `ChangeManagementService.test.ts` proves actor A/key X and actor B/key X are independent; no backend implementation, schema, provider contract, or migration changed |

### Idempotency decision

Backend inspection confirmed the existing durable key is
`(operation, requested_by, idempotency_key)`. The actor is obtained from backend
authentication, never from frontend input. Therefore:

- same actor + key + same payload returns the original result;
- same actor + key + different payload returns 409;
- different actors + the same key use independent namespaces.

The frontend creates one UUID on the first valid submit, reuses it after network/
transport failures, `PROVIDER_UNAVAILABLE`, or another 5xx, and invalidates it after
any scalar or activity mutation. It clears the key on 400/403/409 and after success.

### Required verification

```text
yarn workspace @internal/plugin-change-management test --watchAll=false  PASS (40/40, 10 suites)
yarn workspace @internal/plugin-change-management lint                   PASS
yarn workspace @internal/plugin-change-management build                  PASS
yarn workspace backend test --watchAll=false                             PASS (89/89, 20 suites)
yarn workspace backend lint                                              PASS
git diff --check                                                          PASS
```

### Final review answers

1. Real frontend uses `ChangeManagementApi` — **Yes**.
2. `MockChangeManagementApi` inactive in normal runtime — **Yes**; tests/fixtures only.
3. POST excludes `requestedBy` / `ownerRef` / `systemRef` — **Yes**.
4. `executionPlan` always has at least one activity — **Yes**; UI and backend validation.
5. `responsibleRef` selected as Catalog Group — **Yes**.
6. `activityId` backend-only — **Yes**.
7. Same logical retry reuses `Idempotency-Key` — **Yes** for retryable failures.
8. Form mutation produces a new key — **Yes**, including activity changes.
9. Idempotency scoped to authenticated actor — **Yes**, existing composite key plus explicit test.
10. Canonical backend `changeId` displayed after success — **Yes**, from POST without GET.
11. Provider/ADO/workflow concern leaked into frontend — **No**.
12. End-to-end create path ready for functional review — **Yes: GO**; login and authenticated browser creation are the functional review.

### Deviations and known observations

- No architecture/domain deviation and no ADR-008 change.
- The plan's `PROVIDER_UNAVAILABLE` error code is used exactly; no obsolete `PERSISTENCE_UNAVAILABLE` frontend mapping remains.
- The extra repository-wide `yarn tsc:full` diagnostic (not a required gate) still reports pre-existing third-party Backstage/react-use declaration conflicts and existing Knex typing errors in `changeManagementPlugin.ts`. Package build and both required lints pass.
- Jest emits existing React/MUI `findDOMNode` deprecation warnings; they do not fail tests.
- Authenticated browser login/create was intentionally not automated here; it is the requested functional review gate.

### Commits and gate

- ADO: **`75da44fb46d308e23b1c987e2093636fa4811b92`** on `feat/ado-repo-governance` (baseline `5e4f30e`).
- Bridge: this handoff commit on `main` (baseline `412822bc89df4058e0649c812bb4e7e4296a3c01`).
- Functional review: **GO**.
- Next implementation slice: **STOP** pending architecture review.

## 16. GMUD F2.2 — My Changes List + Change Detail

**Checkpoint:** F2.2 — read UX closing create → discover → open → read. **STOP** —
do not begin F3 (approvals, workflow, real ITSM providers, Teams, CAB, ADO
correlation, evidence upload, editing, activity lifecycle).

### Objective

Make a created GMUD discoverable and readable: a "Minhas GMUDs" list backed by the
platform canonical index, a read-only detail page reachable from that list and from
the post-create success screen, and the smallest backend surface (`GET /changes`)
needed to serve the list without ever calling `IChangeManagementProvider`.

### Architecture decisions (summary)

| Decision | Choice |
|---|---|
| List data source | Platform canonical index (`change_index`, `is_finalized = true` only) — never the provider |
| Detail data source | Unchanged — `indexRecord.providerKey` → `IChangeManagementProvider.get()` |
| List authorization | Re-runs the exact `canReadChange(snapshot, actor)` predicate GET uses — not a parallel rule |
| `ExecutionActivity.responsibleRef` | Grants no read access on list or detail (unchanged policy) |
| List projection | New `ChangeSummary` type — no `providerKey`/`externalId`/`externalUrl`, `activityCount` instead of the full execution plan |
| Repository surface | `ChangeIndexRepository.listReadable(filter)`, `filter: { scope: 'all' } \| { scope: 'actor'; requestedBy; ownerRefs }` — not a generic `ChangeRepository.list()` |
| Pagination | Fixed `limit = 50`, `ORDER BY created_at DESC` — no query language, no filters |
| Frontend routing | Single `page:change-management` extension at `/gmud`; nested `<Routes>` (`GmudRouter`) for `/gmud` (list), `/gmud/new` (create, moved from `/gmud`), `/gmud/:changeId` (detail) via `createSubRouteRef` |
| Provider-failure UX | List stays available (index-backed); detail shows a human-safe 503 message and never renders stale snapshot data as if it were current |
| Display names | Catalog resolution is best-effort; failure/loading falls back to the raw entity ref permanently — never blocks rendering |

See [ADR-006](../adr/ADR-006-change-management-backend-contract.md) "List read
scope (F2.2)" and [ADR-007](../adr/ADR-007-change-record-authority.md) "Clarification
— discovery/listing vs. detail authority" for the full contract.

### Bridge files changed

| Path | Change |
|---|---|
| `docs/adr/ADR-006-change-management-backend-contract.md` | `GET /changes` added to HTTP contract; new "List read scope (F2.2)" subsection; F2.2 addendum |
| `docs/adr/ADR-007-change-record-authority.md` | New "Clarification — discovery/listing vs. detail authority (F2.2)" section |
| `docs/backstage/current-state.md` | F2.2 snapshot — list/detail delivered |
| `docs/backstage/implementation-progress.md` | This section (§16) |
| `docs/ui/gmud-my-changes-screen.md` | New — normative UI contract for the list page |
| `docs/ui/gmud-detail-screen.md` | New — normative UI contract for the detail page |
| `docs/ui/gmud-create-screen.md` | Post-create navigation section added (Ver GMUD / Criar outra GMUD / Voltar para Minhas GMUDs) |

### ADO files changed

| Path | Purpose |
|---|---|
| `packages/backend/src/modules/changeManagement/types.ts` | `ChangeSummary` projection type |
| `packages/backend/src/modules/changeManagement/persistence/types.ts` | `ChangeIndexListFilter` |
| `packages/backend/src/modules/changeManagement/persistence/ChangeIndexRepository.ts` | `listReadable(filter)` added to the interface |
| `packages/backend/src/modules/changeManagement/persistence/KnexChangeIndexRepository.ts` | `listReadable` — `is_finalized = true`, actor OR owner-group predicate, `ORDER BY created_at DESC LIMIT 50` |
| `packages/backend/src/modules/changeManagement/testHelpers.ts` | `FaultInjectingChangeIndexRepository.listReadable` passthrough |
| `packages/backend/src/modules/changeManagement/ChangeManagementService.ts` | `listChanges(actor)` — scope resolution, `canReadChange` re-filter, `toChangeSummary` mapping; zero provider calls |
| `packages/backend/src/modules/changeManagement/ChangeManagementService.list.test.ts` | New — list authorization, content, architecture (no provider calls), provider-failure coverage |
| `packages/backend/src/plugins/changeManagementPlugin.ts` | `GET /changes` route, mounted before `GET /changes/:changeId` |
| `plugins/change-management/src/routes.ts` | `createChangeRouteRef` (`/new`), `changeDetailRouteRef` (`/:changeId`) via `createSubRouteRef` |
| `plugins/change-management/src/alpha.tsx` | Page loader now resolves `GmudRouter` instead of `GmudCreatePage` directly |
| `plugins/change-management/src/components/GmudRouter/GmudRouter.tsx` | New — nested route table for the GMUD area |
| `plugins/change-management/src/components/GmudListPage/GmudListPage.tsx` | New — Minhas GMUDs (`@backstage/core-components` `Table`, `EmptyState`) |
| `plugins/change-management/src/components/GmudDetailPage/GmudDetailPage.tsx` | New — read-only canonical detail + ordered execution plan |
| `plugins/change-management/src/utils/useEntityDisplayNames.ts` | New — best-effort Catalog ref → display-name resolution, ref fallback |
| `plugins/change-management/src/utils/formatChangeWindow.ts` | Previously untracked/unused — now consumed by list and detail |
| `plugins/change-management/src/utils/mapChangeManagementError.ts` | New `mapChangeReadErrorToMessage` (read-flavored copy, separate from the create-flavored mapping) |
| `plugins/change-management/src/api/ChangeManagementApi.ts` / `ChangeManagementClient.ts` / `MockChangeManagementApi.ts` | `listChanges()`, `getChange(changeId)` added to the client contract |
| `plugins/change-management/src/components/GmudCreatePage/GmudCreatedSummary.tsx` | Post-create actions: Ver GMUD, Voltar para Minhas GMUDs, Criar outra GMUD |
| `plugins/change-management/src/index.ts` | Exports `Change`, `ChangeSummary`, the two new sub-route refs |
| `*.test.ts(x)` (list/detail/router pages, client, create-page mocks) | New/updated coverage — see "Tests executed" below |

### Commits

| Repository | Branch | SHA |
|---|---|---|
| ADO `platform-devops-developer-portal` | `feat/ado-repo-governance` | `0b9cb38` |
| Bridge `poc-teams-approval` | `main` | `83a2b79` (reconciliation merge — this section) — first drafted as `424e615` before this checkpoint fetched upstream `f5f131f`; see "Deviations" below |

### Tests executed (ADO)

```bash
yarn workspace backend test --watchAll=false --testPathPatterns=changeManagement
# PASS 9 suites / 56 tests (11 new in ChangeManagementService.list.test.ts)

yarn workspace @internal/plugin-change-management test --watchAll=false
# PASS 13 suites / 58 tests

yarn workspace backend lint                     # PASS
yarn workspace @internal/plugin-change-management lint   # PASS
yarn tsc
# 5 pre-existing errors in changeManagementPlugin.ts (dual-knex-package type
# mismatch between @backstage/backend-plugin-api's bundled knex and the
# workspace knex; present before this checkpoint, on unmodified lines,
# verified via `git stash` against the same file) — zero new errors from F2.2
```

### Persistence verification (checkpoint §18 of the brief)

A temporary, non-committed manual test built the real `ChangeManagementService` +
`DevelopmentProvider` + `KnexChangeIndexRepository` against a **file-backed**
better-sqlite3 database (not `:memory:`) and ran the unmodified production
`createChange` path. Verified — via both the Jest assertions and an independent
`sqlite3` CLI query against the resulting file, outside Node/Knex entirely:

```text
change_id       | provider_key | is_finalized | status    | requested_by       | owner_ref
CHG-2026-000001 | development  | 1            | submitted | user:default/alice | group:default/cloud_azure_devops_payments

change_id       | record_json_bytes
CHG-2026-000001 | 845
```

Confirms `CHG-2026-000001` exists in **both** `change_index` (finalized, correct
`provider_key`) and `development_change_records` (full canonical `Change` as
`record_json`), and that `listChanges` surfaces it as a `ChangeSummary`. No debug
SQL paths were added to production code; the verification script was deleted after
the run.

### Deviations

None between the ADO implementation and ADR-006/ADR-007 (as amended for F2.2) at
time of handoff — `GET /changes` behaves exactly as documented in ADR-006 "List
read scope (F2.2)", and detail routing is unchanged.

**Process deviation, corrected before this commit:** the F2.2 implementation
session began from a local bridge clone pinned to `8c153ce` and did not run
`git fetch origin main` before starting, so it was unaware that §12–§15 above
(the real F2.1–F2.1.3 handoffs) and ADR-008 already existed at `f5f131f`. This
produced two mistakes in the first draft of this handoff, both discovered and
reconciled via a merge before this commit landed:

1. A redundant, independently-reconstructed "F2.1 → F2.1.3 baseline backfill"
   section — discarded in favor of the real §12–§15 above once `f5f131f` was
   fetched. Its technical claims matched the real handoffs on comparison, so no
   architectural correction was needed, only de-duplication.
2. An incorrect claim, repeated in ADR-006, ADR-007, `current-state.md`, and the
   final report given to the user, that **ADR-008 does not exist**. It exists —
   accepted at the F2.1.2 architecture review, 2026-09-01 — and is referenced
   correctly throughout this document and the ADRs as of this commit.

Lesson for future GMUD checkpoints: `git fetch origin <branch>` and diff against
it before starting a bridge documentation session, not only before pushing.

### Unresolved questions

1. Same as ADR-006 §"List read scope (F2.2)": team-wide/enterprise-wide search
   across other actors' changes is deferred to F3 — not attempted here.
2. `created_at` has no dedicated index; acceptable at dev scale, called out as a
   future concern if `change_index` grows large.

### Next recommended slice (F3)

Per the brief's explicit STOP condition — do **not** begin without a separate
architecture review:

1. Approvals / workflow / activity lifecycle statuses
2. Real ITSM providers (SharePoint, Jira, ServiceNow)
3. Teams, CAB scheduling
4. Azure DevOps / pipeline / deployment correlation
5. Evidence upload, editing, deletion
6. Team-wide / enterprise listing and search
7. **STOP** before any of the above without review

**F2.2 handoff complete.** Wait for architecture review before F3.

---

## 17. GMUD F2.2.1 — Participant Read Policy

### Problem

F2.2's read policy (`platform_admin` / requester / `change.ownerRef` team) contradicted
ADR-008 (F2.1.2): `ExecutionActivity.responsibleRef` identifies the Catalog Group
responsible for executing an activity, but F2.1.2 explicitly withheld read access from it.
A team assigned execution responsibility for one activity in a multi-team GMUD (e.g. DBA,
Network on a Payments-owned change) could not read the GMUD that assigned it that
responsibility.

### Decision — change participant read policy

An actor may read a change when **any** hold: `platform_admin` (override), requester,
`ownerRef` team membership, or membership in **any**
`executionPlan.activities[].responsibleRef`. This is the **change participant**
definition. **Read only** — no ownership, approval, CAB, manager approval, deployment
approval, edit, or execution orchestration authority. Full rationale and predicate:
[ADR-006 "Participant read scope (F2.2.1)"](../adr/ADR-006-change-management-backend-contract.md#participant-read-scope-f221).
ADR-008's F2.1.2 decision text is preserved with a superseded-by note, not rewritten — the
meaning of `responsibleRef` itself did not change.

### DB strategy — derived participant index (chosen over post-filter or JSON-aware SQL)

`responsibleRef` values live inside `change_index.execution_plan_json` (TEXT). Three
options were weighed:

- **In-memory post-filter** — drop the SQL predicate, filter every finalized row in the
  service. Smallest diff, but the `LIMIT 50` can no longer be applied in SQL — it becomes a
  full-table read per list call. Rejected as the scalability trap the brief warned against.
- **Dialect-specific JSON filtering** — `json_each` (SQLite) / `jsonb_array_elements`
  (Postgres) inline in the query. No new table, correct `LIMIT`, but two dialect branches,
  and the Postgres path is never exercised by the SQLite-backed test suite — an untested
  production code path. Rejected.
- **Derived participant index table (chosen)** — new
  `change_index_activity_participants(change_id, participant_ref)`, written alongside
  `change_index` inside `KnexChangeIndexRepository.insertPending` (same transaction, no new
  repository class), backfilled by migration for existing rows. One dialect-neutral SQL
  predicate (`change_id IN (SELECT change_id FROM ... WHERE participant_ref IN (...))`),
  `LIMIT` stays a real query bound, indexable. The table is explicitly documented (schema
  comment + ADR-006) as a **derived discovery index, not an authorization source of
  truth** — `canReadChange` still re-checks every returned row against the immutable
  `change_index` snapshot, so the participant table can only narrow candidates, never grant
  access on its own.

### Files changed (ADO)

| File | Change |
|---|---|
| `packages/backend/migrations/change-management/20260901180000_add_activity_participant_index.cjs` | New table `change_index_activity_participants` (composite PK, indexed `participant_ref`, FK to `change_index`); backfills from existing `execution_plan_json` rows |
| `persistence/changeIndexMapper.ts` | New `changeToActivityParticipantRows` (de-duplicated per change) and `getActivityParticipantsQuery` |
| `persistence/KnexChangeIndexRepository.ts` | `insertPending` now writes `change_index` + participant rows in one transaction; `listReadable` adds a third `orWhereIn` clause (subquery on the new table) to the `scope: 'actor'` predicate |
| `persistence/types.ts` | `ChangeIndexListFilter`'s `scope: 'actor'` variant gains `activityResponsibleRefs: string[]` |
| `ChangeManagementService.ts` | `canReadChange` (already the single helper shared by `listChanges` and `getChange`) gains a fourth clause: any `executionPlan.activities[].responsibleRef` membership; `listChanges` passes `activityResponsibleRefs: actor.ownershipEntityRefs` |
| `architecture.test.ts` | Model C guard extended to also forbid `ParticipantRepository` / `TaskRepository` / `WorkflowRepository` in the service source |
| `ChangeManagementService.list.test.ts` | Inverted test asserting the *old* "responsibleRef grants no access" rule replaced with an F2.2.1 participant-policy describe block; `seedIndex` test helper now also writes participant rows |
| `ChangeManagementService.test.ts` | Two new detail-level tests: activity-responsible actor can `getChange`; unrelated actor is `FORBIDDEN` |

No frontend files changed — `canReadChange` is server-side only; the UI needed no change
per §Frontend of the F2.2.1 brief (list/detail rendering and 403/404/503 handling are
policy-agnostic).

### Commits

| Repository | Branch | SHA |
|---|---|---|
| ADO `platform-devops-developer-portal` | `feat/ado-repo-governance` | `6e28611` |
| Bridge `poc-teams-approval` | `main` | `8990dad` |

### Cross-team fixture and results

Per the brief's §13 fixture (`requestedBy: alice`, `ownerRef: payments`, activities:
`dba` / `network` / `payments`):

| Actor | Relationship | Result |
|---|---|---|
| alice | requester | READ |
| payments | `ownerRef` + activity #3 | READ |
| dba | activity #1 only | READ |
| network | activity #2 only (ordering does not affect authorization) | READ |
| hr | none | DENY |
| platform_admin | override | READ |

List/detail consistency was asserted for every class above via `it.each` over readable and
denied actor sets against one seeded change, rather than a single test with conditional
assertions (ESLint `jest/no-conditional-expect` flags branching `expect()` calls — the split
into two `it.each` blocks is both lint-clean and a more explicit per-class test).

### Tests executed (ADO)

```bash
yarn workspace backend test --watchAll=false --testPathPatterns=changeManagement
# PASS 9 suites / 68 tests (12 new: 10 in ChangeManagementService.list.test.ts, 2 in
# ChangeManagementService.test.ts)

yarn workspace @internal/plugin-change-management test --watchAll=false
# PASS 13 suites / 58 tests — unchanged from F2.2; no frontend source touched

yarn workspace backend lint     # PASS
yarn workspace @internal/plugin-change-management lint   # PASS
yarn tsc
# Same 5 pre-existing errors in changeManagementPlugin.ts (dual-knex-package type
# mismatch, present since before F2.2, confirmed via `git diff --stat` touching none of
# its lines) — zero new errors from F2.2.1
```

### Migration verification (checkpoint §6 of the brief)

The real dev database (`packages/backend/data/change-management.sqlite`) was held open by
a running `yarn start` process (confirmed via `lsof`), so it was not touched directly.
Instead, a copy was made to the session scratchpad, the new migration was run against the
copy with a temporary, non-committed script, and the resulting
`change_index_activity_participants` table was queried:

```json
[
  { "change_id": "CHG-2026-000001", "participant_ref": "group:default/cloud_azure_devops_platform_engineering" },
  { "change_id": "CHG-2026-000001", "participant_ref": "group:default/cloud_azure_devops_platform_devops" }
]
```

Confirms the backfill correctly extracted both distinct `responsibleRef` values from the
one real record's `execution_plan_json` (a two-activity plan). The scratch copy and script
were deleted after the run; `git status` on `packages/backend/data/` shows no changes to
the live dev database.

### Performance note (brief §18)

Per list call: one indexed subquery on `participant_ref` (composite PK plus a dedicated
index on that column), then at most 50 rows × up to 20 activities = at most 1000 string
comparisons inside `canReadChange`. `execution_plan_json` was already being parsed per row
in `indexRowToRecord` before this change — no new JSON-parsing cost. Participant rows are
written once at create and never mutated (immutable creation-time snapshot), so the index
never needs updating. Acceptable at F2 scale.

Future concerns, recorded but not implemented (per the brief's explicit "do not
prematurely optimize"): `change_index.created_at` still has no dedicated index (carried
over from the F2.2 handoff); a composite `(participant_ref, change_id)` index on the new
table would only matter at enterprise-wide row counts, well beyond F2 scope.

### Deviations

None between the ADO implementation and ADR-006 (as amended for F2.2.1) at time of
handoff — the four-clause participant predicate, the read-only semantics, and the
DB strategy all match what is documented.

One test intentionally inverted rather than deleted: `ChangeManagementService.list.test.ts`
previously asserted `'does not grant access via ExecutionActivity.responsibleRef'`
(the F2.1.2/F2.2 policy). That test's premise is now false by design; it was replaced with
an equivalent-shaped set of tests asserting the opposite, under a new
`'F2.2.1 — participant read policy'` describe block, rather than silently deleted.

### Unresolved questions

1. Team-wide/enterprise-wide search across other actors' changes remains deferred to F3 —
   unchanged from F2.2.
2. `created_at` still has no dedicated index — unchanged from F2.2, still acceptable at
   dev scale.
3. New governance roles (`change.read.all`, Change Manager, Auditor) remain deferred to F3
   — unchanged from the brief's explicit scope.

### Next recommended slice (F3)

Unchanged from the F2.2 handoff — per the brief's explicit STOP condition, do **not** begin
without a separate architecture review:

1. Approvals / workflow / activity lifecycle statuses
2. Real ITSM providers (SharePoint, Jira, ServiceNow)
3. Teams, CAB scheduling
4. Azure DevOps / pipeline / deployment correlation
5. Evidence upload, editing, deletion
6. Team-wide / enterprise listing and search
7. New governance roles (`change.read.all` / Change Manager / Auditor)
8. **STOP** before any of the above without review

**F2.2.1 handoff complete.** Wait for architecture review before F3.

---

## 18. GMUD F3.0 — Change Authorization Architecture

**Checkpoint:** Architecture only. No application code, ADO implementation,
database migration, route, frontend component, Teams integration, CAB UI, pipeline
enforcement, or real ITSM provider was created or modified.

### Baseline verification

- ADO implementation branch: `feat/ado-repo-governance`
- ADO implementation HEAD: `6e28611` (exact F2.2.1 baseline)
- Bridge branch: `main`
- Bridge baseline before F3.0: `692e697`
- `git fetch origin main` completed in the GitHub bridge and local
  `HEAD == origin/main == 692e697` before architecture work
- Pre-existing untracked `.vscode/` directories were not touched

### Decision summary

[ADR-009](../adr/ADR-009-change-authorization-model.md) proposes:

1. versioned deterministic policy evaluated at submission;
2. a historically stable authorization instance/round containing effective
   requirements and resolved-principal snapshots;
3. policy-generated requirements plus additive-only mandatory requirements;
4. immutable `approved` / `rejected` decisions with actor authorization evidence;
5. a minimal Change lifecycle separate from approval progress;
6. `authorized` as completed pre-execution governance and `executable now` as a
   separate runtime window/lifecycle/target evaluation;
7. generic emergency pre-approvers plus non-blocking post-execution CAB review;
8. CAB as one collective authority decision by default;
9. Teams as a future individual-decision channel and Backstage as the preferred
   future CAB Workbench UI, neither authoritative;
10. a provider-neutral execution request/eligibility boundary consumed by future
    pipelines;
11. Model C retained with a bounded platform-owned authorization ledger adjacent
    to the canonical index;
12. DevOps removed from happy-path per-deployment approval and retained as policy,
    control, reliability, observability, and exception owner.

### Authority correction

ADR-001, ADR-004, and ADR-005 are retained as historical ADO-centric POC records.
Cross-reference notes make the supersession conditional on ADR-009 acceptance; no
existing implementation is represented as already migrated. ADR-007 Model C is
not reopened: operational GMUD content remains provider-authoritative, while the
platform owns only the authorization evidence needed to make platform execution
decisions.

### Files changed (bridge only)

| Path | Change |
|---|---|
| `docs/adr/ADR-009-change-authorization-model.md` | New — complete F3 authorization architecture and review answers |
| `docs/adr/README.md` | ADR-009 index and proposed target flow; historical ADO ADR context |
| `docs/adr/ADR-001-azure-devops-approval-authority.md` | Proposed-supersession banner; historical decision preserved |
| `docs/adr/ADR-002-backstage-change-onramp.md` | Proposed F3 authority/workbench refinement note |
| `docs/adr/ADR-004-teams-delegated-approval-identity.md` | Proposed-supersession banner for Teams-to-ADO path |
| `docs/adr/ADR-005-cab-scheduling-and-concurrency.md` | Proposed-supersession banner for CAB-as-ADO-check model |
| `docs/adr/ADR-007-change-record-authority.md` | Model C authorization-authority refinement note |
| `docs/ui/gmud-create-screen.md` | Clarifies current “gestor” rail copy is not canonical future policy; no UI change authorized |
| `docs/backstage/current-state.md` | Proposed F3.0 architecture snapshot and gate |
| `docs/backstage/implementation-progress.md` | This architecture-only handoff |

### Rejected alternatives

ADR-009 explicitly rejects: DevOps final approval, moving that button to Teams,
hardcoded manager/CAB/DevOps workflows, corporate-role fields, N CAB clicks by
default, sole ITSM authorization, canonical ADO Environment approval state,
workflow-specific status explosion, a generic BPM engine, and a policy DSL from
day one.

### Open decisions and gate

Nine decisions are mandatory before F3.1 planning: exact policy matrix; policy and
selector publication; who may add mandatory requirements; rejection/resubmission;
cancellation; CAB recorder/evidence; emergency retrospective behavior; emergency
separation of duty; and authorization/auditor permissions.

**Outcome:** F3.0 architecture is ready for stakeholder review. **NO-GO for F3.1
implementation planning** until those decisions are resolved and ADR-009 is
accepted. Wait for architecture review; do not implement F3.1.

### ADO files changed

None. ADO remains at implementation baseline `6e28611`.

### Deviations

None from the architecture-only scope. The initial `git fetch origin main` attempted
from the ADO working tree could not authenticate because its `origin` is Azure
DevOps; the canonical GitHub bridge was then located, fetched successfully, and
verified at `692e697` before any documentation edit.

### Architect handoff

The architecture proposal was published in bridge commit `7c050a7`. The active
[F3 architect review brief](../architect-review-f3-change-authorization.md) now
summarizes the decision, invariants, nine mandatory product/governance decisions,
deferred scope, and NO-GO gate. The older Teams-to-ADO identity brief is explicitly
retained as historical POC evidence rather than an active implementation choice.

---

## 19. GMUD F3.0.1 — Authorization Architecture Convergence

**Checkpoint:** Architecture and documentation only. No application code, ADO
implementation, database migration, route, frontend component, Teams integration,
CAB UI, pipeline enforcement, or real ITSM provider was created or modified.

### Baseline verification

- ADO implementation remained on branch `feat/ado-repo-governance` at `6e28611`
- Bridge branch: `main`
- `git fetch origin main` completed before edits
- Bridge `HEAD == origin/main == FETCH_HEAD == 6398d28` before edits
- Existing untracked `.vscode/` was preserved

### Architecture corrections

1. Removed `authorized` from Change lifecycle. `AUTHORIZED` now exists only in
   derived `AuthorizationEvaluation`; lifecycle and authorization may legitimately
   diverge.
2. Added derived `GovernanceEvaluation` over mandatory post-execution requirements
   with `NOT_APPLICABLE`, `PENDING`, `COMPLIANT`, and `NON_COMPLIANT`. Retrospective
   rejection or SLA breach creates non-compliance evidence without invalidating a
   historically authorized execution.

### Final state model

| Dimension | Values | Authority/derivation |
|---|---|---|
| Change lifecycle | `submitted`, `executing`, `completed`, `rejected`, `cancelled` | Projection rebuilt from accepted lifecycle evidence; never contains `authorized` |
| Authorization | `PENDING`, `AUTHORIZED`, `REJECTED` | Derived from mandatory pre-execution requirements in the current round |
| Governance | `NOT_APPLICABLE`, `PENDING`, `COMPLIANT`, `NON_COMPLIANT` | Derived from mandatory post-execution requirements and snapshotted SLA policy |
| Execution eligibility | `ALLOW`, `DENY` with reasons | Point-in-time runtime result; never a lifecycle status |

Canonical facts include immutable round snapshots, policy/selector provenance,
requirements, principal snapshots, decisions, and append-only audit/lifecycle
evidence. Read-oriented lifecycle/evaluation values may be materialized only as
rebuildable projections.

An eligibility `ALLOW` does not start execution. Only an accepted governed
execution-start event moves the lifecycle to `executing`; accepted completion
evidence moves it to `completed`. Completed means execution completed and may
coexist with governance `PENDING`.

### Resolved product/governance decisions

| Area | F3 MVP decision |
|---|---|
| Normal low | One configured mandatory pre-execution approval |
| Normal medium/high | Configured primary approval plus CAB pre-execution approval |
| Emergency | Distinct configured A/B human decision actors pre-execution plus mandatory non-blocking CAB retrospective |
| Additional requirements | Dedicated backend permission; before/as submission; additive only; users or configured authorities; immutable after submission |
| Rejection/resubmission | Same `changeId`, next monotonic round, new immutable Change/policy/principal snapshot; prior round preserved |
| Current round | Greatest valid server-issued round number; no mutable current flag |
| Cancellation | Before start: requester/owner/governance-admin; after start: governance-admin only; blocks new execution, not running work |
| CAB | One collective authority decision recorded by one currently authorized actor with complete attestation evidence |
| Retrospective SLA | Rule/version/anchor snapshotted; deadline derived from server completion time; rejection or miss = `NON_COMPLIANT` |
| Permissions | Participant read, audit read, decide, CAB record, add requirement, cancel, policy admin, governance read-all are separate |
| Policy publication | Platform/DevOps authors/operates, governance approves, immutable version published; existing rounds never re-evaluate mutable current config |

No corporate title, e-mail address, current employee name, ADO approval ID, Teams
identity, or ITSM identifier is canonical authorization semantics. Selector overlap
for emergency A/B fails submission closed; F3.1 has no exception mechanism.

### Model C authority

Platform Change Management owns policy/selector versions, authorization rounds,
requirements, principal snapshots, decisions, derived authorization/governance,
accepted lifecycle evidence needed for eligibility, and eligibility audit. The
configured ITSM provider continues to own full operational GMUD detail,
attachments, and provider workflow. ITSM/ADO/Teams/Backstage copies are
non-authoritative projections or interaction context. The authorization ledger
remains adjacent to the canonical index and does not replace
`IChangeManagementProvider`.

### F3.1 planning boundary

**Authorization Ledger Foundation** may plan:

- round/requirement/principal-snapshot/decision/audit persistence;
- deterministic published policy configuration and selector resolution;
- submission-time round generation, additive-requirement authorization,
  emergency separation-of-duty validation, and new-round domain semantics;
- server-authoritative idempotent decision commands;
- pure authorization/governance evaluators;
- permission-filtered Change-detail authorization representation and separated
  backend permission enforcement.

Explicitly excluded: Teams, CAB Workbench, ADO enforcement, public execution-check
transport, execution lifecycle integration, automatic SLA/escalation jobs, real
ITSM providers, generic DSL/BPM, break-glass, post-submission requirement changes,
reversal, abstention, expiry, and technical stop-execution behavior.

### Bridge files changed

| Path | Change |
|---|---|
| `docs/adr/ADR-009-change-authorization-model.md` | Accepted converged architecture, resolved policy/governance decisions, four-dimensional state model, 32 review answers, F3.1 boundary |
| `docs/architect-review-f3-change-authorization.md` | Replaced nine unresolved decisions with accepted outcomes, deferred implementation choices, and planning gate |
| `docs/backstage/current-state.md` | Separates F2.2.1 implemented, F3 accepted architecture, and F3.1 not implemented |
| `docs/backstage/implementation-progress.md` | This F3.0.1 checkpoint |
| `docs/adr/README.md` | ADR-009 accepted status and current target flow |
| `docs/adr/ADR-001-azure-devops-approval-authority.md` | Historical ADO authority explicitly superseded |
| `docs/adr/ADR-002-backstage-change-onramp.md` | Accepted platform authorization boundary; historical ADO wording labeled |
| `docs/adr/ADR-004-teams-delegated-approval-identity.md` | Historical Teams-to-ADO target explicitly superseded |
| `docs/adr/ADR-005-cab-scheduling-and-concurrency.md` | Historical CAB-as-ADO-check target explicitly superseded |
| `docs/adr/ADR-007-change-record-authority.md` | Model C authority table aligned to accepted ADR-009 |
| `docs/architect-decision-teams-approval-identity.md` | Historical Teams POC brief points to accepted ADR-009 target |
| `docs/ui/gmud-create-screen.md` | Accepted-but-not-implemented F3 policy note; current UI wording remains informational |

### ADR status and gate

ADR-009 status is **Accepted**. The two conceptual corrections and all nine
must-decide items are resolved sufficiently for the next planning checkpoint.

**GO for F3.1 implementation planning.**

**NO-GO for F3.1 implementation** until a reviewed implementation plan explicitly
authorizes code, migrations, routes, or integration work.

### Deviations and remaining questions

No architecture-scope deviation. ADO remains unchanged at `6e28611`.

No remaining question blocks F3.1 planning. Literal permission names, persistence
and transport shapes, configuration module layout, release mechanics, retention,
and error codes remain implementation-planning decisions. SLA duration remains
policy configuration. Channel UX/authentication, enforcement, provider projection,
escalation automation, advanced CAB semantics, and break-glass remain later-slice
questions.

---
