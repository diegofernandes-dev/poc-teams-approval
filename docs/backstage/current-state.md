# Backstage Current-State Architecture Inventory

> **Purpose:** Architecture handoff for external review of the GMUD MVP integration into the corporate Internal Developer Portal.
>
> **Source of truth (implementation):** Azure DevOps repository `platform-devops-developer-portal`
>
> **Inspection date:** 2026-08-29
>
> **Discovery method:** Read-only code and configuration review (no runtime deployment inspection)
>
> **Related bridge documentation:**
> - [ADR-002 — Backstage is the change-request onramp](../adr/ADR-002-backstage-change-onramp.md)
> - [ADR-003 — Change management is provider-agnostic](../adr/ADR-003-provider-agnostic-change-management.md)
> - [ADR-004 — Teams delegated approval identity](../adr/ADR-004-teams-delegated-approval-identity.md)
> - [ADR-005 — CAB scheduling and concurrency](../adr/ADR-005-cab-scheduling-and-concurrency.md)
> - [GMUD create screen UI contract](../ui/gmud-create-screen.md)
> - [Future GMUD context enrichment](../future-gmud-context-enrichment.md) (partially superseded by ADR-003)

---

## 1. Executive summary

The Azure DevOps Backstage repository is a mature **Backstage 1.51.0** Internal Developer Portal with Fase 1 (auth, org sync, RBAC, catalog, TechDocs, ADO access) largely implemented and Fase 2 (.NET service scaffolding) in place.

| Dimension | Current state |
|-----------|---------------|
| Backstage version | 1.51.0 (`backstage.json`) |
| Backend system | **New** — `createBackend()` from `@backstage/backend-defaults` |
| Frontend system | **New** — `createApp()` from `@backstage/frontend-defaults` + extension blueprints |
| Package manager | Yarn 4.4.1 |
| Node engines | 22 \|\| 24 |
| Auth | Microsoft Entra ID only |
| RBAC | Community RBAC plugin + Permission Framework |
| Azure DevOps | Scaffolder provisioning, project access API, governance actions |
| GMUD / change management | **Not implemented** — no templates, plugins, APIs, nav items, or `changeId` references |

The existing Scaffolder is scoped exclusively to **.NET 10 service creation** (four templates). It does not implement the GMUD "Create Production Change" flow described in bridge ADR-002.

---

## 2. Platform stack and tooling

### Evidence

| Item | Location | Value |
|------|----------|-------|
| Backstage version | `backstage.json` | `1.51.0` |
| Package manager | `package.json` → `packageManager` | `yarn@4.4.1` |
| Node engines | `package.json` → `engines.node` | `22 \|\| 24` |
| TypeScript | `package.json` devDependencies | `~5.8.0` |
| CLI | `package.json` devDependencies | `@backstage/cli` `^0.36.2` |

### Quality commands

From root `package.json`:

- `yarn lint` / `yarn lint:all`
- `yarn tsc` / `yarn tsc:full`
- `yarn test` / `yarn test:all`
- `yarn backstage-cli config:check`
- `yarn test:e2e` (Playwright)

### Repository layout

```text
platform-devops-developer-portal/
├── packages/app/              Frontend (4 custom modules)
├── packages/backend/        Backend (2 custom plugins, 8 custom modules)
├── templates/                 4 .NET 10 Software Templates
├── examples/showcase/         Catalog/TechDocs reference bundle
├── docs/adrs/                 11 ADRs (0001–0008, 0010–0012; no 0009)
├── docs/runbooks/ado-governance/  ADO governance automation scripts
├── app-config.yaml            Development configuration
├── app-config.production.yaml Production overrides
└── .env.example               Environment variable name reference
```

No local frontend or backend plugins exist under `plugins/` (only a README).

---

## 3. Backend architecture

### 3.1 Backend system

**Verdict: new Backstage backend system.** No legacy `createServiceBuilder` or `PluginEnvironment` pattern.

Evidence — `packages/backend/src/index.ts`:

- Entry: `createBackend()` from `@backstage/backend-defaults`
- Registration: `backend.add(import('...'))` dynamic imports
- Extensions: `createBackendPlugin` / `createBackendModule` from `@backstage/backend-plugin-api`
- Startup: `backend.start()`

### 3.2 Stock plugins registered

| Plugin area | Package |
|-------------|---------|
| App | `@backstage/plugin-app-backend` |
| Proxy | `@backstage/plugin-proxy-backend` |
| Scaffolder | `@backstage/plugin-scaffolder-backend` |
| Scaffolder — Azure | `@backstage/plugin-scaffolder-backend-module-azure` |
| Scaffolder — GitHub | `@backstage/plugin-scaffolder-backend-module-github` |
| Scaffolder — notifications | `@backstage/plugin-scaffolder-backend-module-notifications` |
| TechDocs | `@backstage/plugin-techdocs-backend` |
| Auth | `@backstage/plugin-auth-backend` |
| Catalog | `@backstage/plugin-catalog-backend` |
| Catalog — scaffolder entity model | `@backstage/plugin-catalog-backend-module-scaffolder-entity-model` |
| Catalog — MS Graph | `@backstage/plugin-catalog-backend-module-msgraph` |
| Catalog — logs | `@backstage/plugin-catalog-backend-module-logs` |
| Permission | `@backstage/plugin-permission-backend` |
| RBAC | `@backstage-community/plugin-rbac-backend` |
| Search | `@backstage/plugin-search-backend` |
| Search — PostgreSQL | `@backstage/plugin-search-backend-module-pg` |
| Search — catalog collator | `@backstage/plugin-search-backend-module-catalog` |
| Search — techdocs collator | `@backstage/plugin-search-backend-module-techdocs` |
| Kubernetes | `@backstage/plugin-kubernetes-backend` |
| Notifications | `@backstage/plugin-notifications-backend` |
| Signals | `@backstage/plugin-signals-backend` |
| MCP actions | `@backstage/plugin-mcp-actions-backend` |

### 3.3 Custom backend plugins

| Plugin ID | File | Key symbols | Purpose |
|-----------|------|-------------|---------|
| `ado-project-access` | `packages/backend/src/plugins/adoProjectAccessPlugin.ts` | `listTeamProjectsFromCatalogSystems`, `isPlatformAdminUser` | `GET /api/ado-project-access/projects` — lists Team Projects from catalog Systems filtered by user ownership |
| `catalog-entity-files` | `packages/backend/src/plugins/catalogEntityFilesPlugin.ts` | `readEntityMarkdown`, `resolveEntityDirectory` | `GET /api/catalog-entity-files/:kind/:namespace/:name/{readme\|changelog}` — serves markdown from local file or ADO URL source |

### 3.4 Custom backend modules

| Module ID | Plugin | File | Purpose |
|-----------|--------|------|---------|
| `microsoft-entra-provider` | `auth` | `modules/authMicrosoftEntraModule.ts` | Registers Microsoft OAuth provider with custom Entra sign-in resolvers |
| `entra-ownership-resolver` | `auth` | `modules/entraOwnershipResolverModule.ts` | Issues JWT `ent` claims with ownership entity refs |
| `idp-provisioner` | `scaffolder` | `modules/idpProvisioner/idpProvisionerModule.ts` | Registers custom scaffolder actions for ADO provisioning |
| `dotnet-naming` | `scaffolder` | `modules/dotnetNaming/dotnetNamingModule.ts` | Registers `idp:dotnet-project-name` action |
| `team-project-group-display` | `catalog` | `modules/teamProjectGroupDisplayModule.ts` | MS Graph group transformer + `TeamProjectGroupDisplayNameProcessor` |
| `stale-entra-governance-group-cleanup` | `catalog` | `modules/staleEntraGovernanceGroupCleanupModule.ts` | Startup purge of mistakenly synced Entra governance groups |
| `org-catalog-bootstrap` | `catalog` | `modules/orgCatalogBootstrapModule.ts` | Waits for Entra org membership relations at startup |
| `template-executor-role-seed` | `permission` | `modules/templateExecutorRoleSeed.ts` | Seeds `role:default/template_executor` in RBAC database |

### 3.5 Supporting libraries (not directly registered)

| File | Key symbols |
|------|-------------|
| `modules/entraOwnership.ts` | `ENTRA_TP_GROUP_PREFIX`, `resolveEntraOwnershipEntityRefs`, `isPlatformAdminUser` |
| `modules/entraSignInResolvers.ts` | `entraSignInResolvers.entraUserWithTeamProjectMembership` |
| `modules/entraGroupTaxonomy.ts` | `isEntraGovernanceCatalogGroup`, `isEntraTeamCatalogGroup` |
| `modules/teamProjectGroupDisplay.ts` | `applyTeamProjectGroupDisplayName`, `isEntraTeamProjectGroup` |
| `modules/adoProjectAccess/teamProjectFromSystem.ts` | `ADO_TEAM_PROJECT_ANNOTATION`, `listTeamProjectsFromCatalogSystems` |
| `modules/adoProjectAccess/teamProjectFromGroup.ts` | `teamProjectSlugFromGroupRef`, `teamProjectDisplayNameFromGroupRef` |
| `modules/idpProvisioner/adoAzureClient.ts` | `createAzureDevOpsWebApi`, `getAzureDevOpsPatAuthHeaders` |
| `modules/idpProvisioner/adoIdentityResolver.ts` | `resolveAdoGroupIdentityId`, `findAdoGroupDescriptorByDisplayName` |
| `modules/idpProvisioner/adoGovernanceGroupNaming.ts` | `resolveTeamProjectGroupName`, `DEFAULT_GROUP_NAMING_PATTERNS` |

### 3.6 Key backend package versions

From `packages/backend/package.json`:

| Package | Version |
|---------|---------|
| `@backstage/backend-defaults` | `^0.17.1` |
| `@backstage/backend-plugin-api` | `^1.9.1` |
| `@backstage-community/plugin-rbac-backend` | `^7.13.0` |
| `@backstage/plugin-catalog-backend` | `^3.7.0` |
| `@backstage/plugin-catalog-backend-module-msgraph` | `^0.10.2` |
| `@backstage/plugin-auth-backend` | `^0.29.0` |
| `@backstage/plugin-auth-backend-module-microsoft-provider` | `^0.3.15` |
| `@backstage/plugin-scaffolder-backend` | `^4.0.0` |
| `@backstage/plugin-scaffolder-backend-module-azure` | `^0.2.21` |
| `@backstage/plugin-techdocs-backend` | `^2.2.0` |
| `@backstage/plugin-permission-backend` | `^0.7.12` |
| `@backstage/plugin-search-backend-module-pg` | `^0.5.55` |
| `@backstage/plugin-kubernetes-backend` | `^0.21.4` |
| `@backstage/plugin-mcp-actions-backend` | `^0.1.13` |
| `azure-devops-node-api` | `15.1.2` |
| `better-sqlite3` | `^12.0.0` (dev database) |
| `pg` | `^8.11.3` (production database) |
| `express` | `^5.2.1` |
| `zod` | `^4.4.3` |

### 3.7 Installed but not registered

Dependencies present in `packages/backend/package.json` but **not** added via `backend.add()` in `index.ts`:

- `@backstage/plugin-auth-backend-module-github-provider`
- `@backstage/plugin-auth-backend-module-guest-provider`

---

## 4. Frontend architecture

### 4.1 Frontend system

**Verdict: new Backstage frontend system.** Uses `@backstage/frontend-defaults` + extension blueprints, not legacy `FlatRoutes`.

Evidence — `packages/app/src/index.tsx`:

- Imports `@backstage/ui/css/styles.css`
- Mounts via `App.createRoot()`

Evidence — `packages/app/src/App.tsx`:

```typescript
export default createApp({
  features: [
    catalogPlugin,
    scaffolderPlugin,
    rbacPlugin,
    navModule,
    signInModule,
    scaffolderModule,
    catalogEntityTabsModule,
  ],
});
```

### 4.2 Custom frontend modules

| Module | File | Key symbols | Purpose |
|--------|------|-------------|---------|
| Auth | `modules/auth/signInPage.tsx` | `signInModule`, `signInPage` | Microsoft Entra ID sign-in via `SignInPageBlueprint` |
| Nav | `modules/nav/Sidebar.tsx` | `SidebarNavContent`, `SidebarContent` | Permission-aware sidebar; RBAC and scaffolder permission gates |
| Scaffolder field | `modules/scaffolder/TeamProjectPicker.tsx` | `TeamProjectPicker`, `TeamProjectValue` | Custom `ui:field` loading projects from `ado-project-access` API |
| Catalog tabs | `modules/catalogEntityTabs/` | `catalogEntityTabsModule`, `EntityMarkdownTab` | README and Changelog entity content tabs for `kind:component` |

### 4.3 Auto-discovered plugins

Via `app.packages: all` in `app-config.yaml`, additional plugin UI is discovered from `packages/app/package.json` dependencies without explicit `App.tsx` imports:

- `@backstage/plugin-api-docs`
- `@backstage/plugin-catalog-graph`
- `@backstage/plugin-catalog-import`
- `@backstage/plugin-kubernetes`
- `@backstage/plugin-notifications`
- `@backstage/plugin-org`
- `@backstage/plugin-search`
- `@backstage/plugin-signals`
- `@backstage/plugin-techdocs`
- `@backstage/plugin-user-settings`
- `@backstage/plugin-app-visualizer`

### 4.4 Navigation and routes

Routes are **not declared in frontend code**; they come from plugin page extensions.

Config-driven routing (`app-config.yaml`):

- `page:catalog` mounted at `/` (root, not `/catalog`)
- `entity-card:readme` disabled (replaced by custom tab)
- `entity-content:techdocs` filtered to `kind:component`

Custom sidebar (`modules/nav/Sidebar.tsx`) references:

| Extension ID | Typical path | Gating |
|--------------|--------------|--------|
| `page:catalog` | `/` | Always visible |
| `page:scaffolder` | `/create` | `scaffolder.template.execute` permission |
| `page:rbac` | `/rbac` | RBAC API authorization |
| `page:catalog-import` | `/catalog-import` | `catalog.entity.create` permission |
| `page:user-settings` | `/settings` | Settings group |

**No GMUD navigation entry, page, or route exists.** This conflicts with the bridge UI spec (`docs/ui/gmud-create-screen.md`) which shows `GMUD` selected in the sidebar.

### 4.5 Key frontend package versions

From `packages/app/package.json`:

| Package | Version |
|---------|---------|
| `@backstage/frontend-defaults` | `^0.5.2` |
| `@backstage/frontend-plugin-api` | `^0.17.0` |
| `@backstage-community/plugin-rbac` | `^1.53.1` |
| `@backstage/plugin-catalog` | `^2.0.5` |
| `@backstage/plugin-scaffolder` | `^1.37.0` |
| `@backstage/plugin-techdocs` | `^1.17.6` |
| `@backstage/plugin-auth` | `^0.1.8` |
| `@backstage/core-components` | `^0.18.10` |
| `@backstage/ui` | `^0.15.0` |
| `react` / `react-dom` | `^18.0.2` |

### 4.6 Branding

| Area | State |
|------|-------|
| App title | `Platform DevOps Developer Portal` (`app-config.yaml`) |
| Organization | `Platform DevOps` |
| Logo | Custom SVG in `modules/nav/LogoFull.tsx`, `LogoIcon.tsx` (tint `#7df3e1`) |
| Theme | Default Backstage UI — no custom `ThemeProvider` or `app.theme` config |
| PWA manifest | Still default "Backstage" naming (`packages/app/public/manifest.json`) |

---

## 5. Configuration model

### 5.1 Development (`app-config.yaml`)

Structure only — all sensitive values use environment variable references.

```text
app
├── title, baseUrl
├── packages: all
└── extensions
    ├── page:catalog (path: /)
    ├── entity-card:readme: false
    └── entity-content:techdocs (filter: kind:component)

organization
└── name

backend
├── auth.keys[].secret          → ${BACKEND_SECRET}
├── baseUrl, listen.port
├── database (client: better-sqlite3)
├── cors, csp
└── actions.pluginSources [auth, catalog, scaffolder]

integrations
├── azure[].host, credentials[].organizations, personalAccessToken → ${AZURE_DEVOPS_PAT}
└── github[].host, token → ${GITHUB_TOKEN}

techdocs
├── builder: local
├── generator.runIn: docker
└── publisher.type: local

auth
├── environment: development
├── providers.microsoft.development
│   ├── clientId, clientSecret, tenantId → ${AZURE_*}
│   └── signIn.resolvers[].resolver: entraUserWithTeamProjectMembership
└── experimentalClientIdMetadataDocuments.enabled: false

adoProjectAccess
└── organization

idpProvisioner
├── organization, defaultBranch, pipelineYamlPath, agentQueueName
└── governance
    ├── mode: hybrid
    ├── protectedBranch, minimumApproverCount, mergeStrategy
    ├── blockPipelineFileChanges, buildValidationDisplayName
    └── groupNaming (prReviewers, deployHomolog, deployProduction, platformPipelinesGroup)

idpCatalog
├── organization, project, repository, defaultBranch
├── catalogInfoPath, componentsPath

catalog
├── import (entityFilename, pullRequestBranchName)
├── rules[].allow [Component, System, API, Resource, Location, User, Group, Template]
├── providers.microsoftGraphOrg.default
│   ├── tenantId, clientId, clientSecret → ${AZURE_*}
│   ├── user.filter, group.filter (allowlist)
│   └── schedule (PT1H)
└── locations[] (RBAC placeholder, 4 templates, showcase, ADO catalog URL)

permission
├── enabled: true
└── rbac
    ├── pluginsWithPermission [catalog, scaffolder, permission]
    ├── policies-csv-file, conditionalPoliciesFile
    ├── policyFileReload: true (dev only)
    └── admin (superUsers, users)

mcpActions
├── name, description
```

### 5.2 Production overrides (`app-config.production.yaml`)

| Key | Development | Production |
|-----|-------------|------------|
| `auth.environment` | `development` | `production` |
| `backend.database.client` | `better-sqlite3` | `pg` (PostgreSQL via `${POSTGRES_*}`) |
| `techdocs.builder` | `local` | `external` |
| `techdocs.publisher.type` | `local` | `awsS3` (`${TECHDOCS_S3_BUCKET}`, `${AWS_REGION}`) |
| `catalog.locations` | Templates, showcase, examples | RBAC placeholder + ADO catalog URL only |
| `permission.rbac.policyFileReload` | `true` | absent |

### 5.3 Environment variable names

From `.env.example` (names only, no values):

| Variable | Purpose |
|----------|---------|
| `AZURE_TENANT_ID` | Entra ID tenant |
| `AZURE_CLIENT_ID` | Entra app registration client ID |
| `AZURE_CLIENT_SECRET` | Entra app registration secret |
| `BACKEND_SECRET` | Backstage backend service-to-service auth |
| `AZURE_DEVOPS_PAT` | Azure DevOps integration (scaffolder, provisioning) |
| `GITHUB_TOKEN` | GitHub integration (placeholder) |
| `TECHDOCS_S3_BUCKET` | TechDocs S3 bucket (production) |
| `AWS_REGION` | AWS region for TechDocs S3 |
| `POSTGRES_HOST` | PostgreSQL host (production) |
| `POSTGRES_PORT` | PostgreSQL port (production) |
| `POSTGRES_USER` | PostgreSQL user (production) |
| `POSTGRES_PASSWORD` | PostgreSQL password (production) |

---

## 6. Identity, ownership, and RBAC

### 6.1 Authentication flow

```text
Microsoft Entra ID OAuth
        |
        v
entraUserWithTeamProjectMembership (custom sign-in resolver)
        |
        v
Catalog User lookup by graph.microsoft.com/user-id annotation
        |
        v
Requires membership in CLOUD_AZURE_DEVOPS_* Entra group
        |
        v
entraOwnershipResolver → JWT with ent ownership claims
```

Evidence:

- `modules/authMicrosoftEntraModule.ts` — wraps `@backstage/plugin-auth-backend-module-microsoft-provider`
- `modules/entraSignInResolvers.ts` — `entraUserWithTeamProjectMembership`
- `modules/entraOwnershipResolverModule.ts` — `createEntraOwnershipResolver`
- `modules/entraOwnership.ts` — `ENTRA_TP_GROUP_PREFIX` = `group:default/cloud_azure_devops_`

### 6.2 Team Project ownership model

Per ADR 0005 (Backstage repo):

- **1 Team Project = 1 System = 1 Entra group owner**
- Components, APIs, Resources share the System owner
- System annotation: `azure.devops.com/project`

### 6.3 RBAC roles

From `packages/backend/config/rbac/rbac-policy.csv`:

| Role | Scope |
|------|-------|
| `authenticated` | Baseline; catalog read scoped to User/Group via conditional policies |
| `contributor` | Team Project members; catalog read/update via `IS_ENTITY_OWNER` |
| `template_executor` | Scaffolder execution; seeded at startup with placeholder user |
| `platform_admin` | Full catalog + scaffolder + RBAC policy management |

Role assignments (Entra groups → roles):

- `group:default/cloud_azure_devops_platform_devops` → `platform_admin`, `contributor`, `authenticated`
- `group:default/cloud_azure_devops_platform_engineering` → `contributor`, `authenticated`

Policy admin: `group:default/cloud_azure_devops_platform_devops`

Conditional policies: `packages/backend/config/rbac/conditional-policies.yaml`

### 6.4 Group taxonomy (ADR 0012)

| Type | Location | Examples |
|------|----------|----------|
| Team (Tipo A) | Entra ID + MS Graph sync | `CLOUD_AZURE_DEVOPS_GARANTIA` |
| Governance (Tipo B) | ADO VSTS only | `Pull Request Reviewers`, `Homologation Deploy Approvers`, `Production Deploy Approvers` |

`staleEntraGovernanceGroupCleanupModule` removes governance groups mistakenly synced from Entra at startup.

---

## 7. Azure DevOps integrations

### 7.1 Project access API

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/api/ado-project-access/projects` | GET | Lists Team Projects from catalog `System` entities with `azure.devops.com/project` annotation, filtered by user `ownershipEntityRefs` unless platform admin |

Frontend consumer: `TeamProjectPicker` (`packages/app/src/modules/scaffolder/TeamProjectPicker.tsx`)

### 7.2 Scaffolder actions

| Action ID | File | Purpose |
|-----------|------|---------|
| `idp:dotnet-project-name` | `modules/dotnetNaming/createDotNetProjectNameAction.ts` | Kebab-case repo name → PascalCase .NET project name |
| `idp:ado-pipeline-create` | `modules/idpProvisioner/createAdoPipelineAction.ts` | Create or reuse YAML pipeline in ADO |
| `idp:ado-repo-governance` | `modules/idpProvisioner/createAdoRepoGovernanceAction.ts` | Apply branch policies (hybrid/perRepo/minimal modes) |
| `idp:catalog-register-pr` | `modules/idpProvisioner/registerIdpCatalogAction.ts` | Open PR in corporate catalog repo (optional) |
| `publish:azure` | Stock `@backstage/plugin-scaffolder-backend-module-azure` | Create ADO repository |
| `catalog:register` | Stock scaffolder | Register component in current portal instance |

### 7.3 Governance mode

Current configuration: **hybrid** (`idpProvisioner.governance.mode: hybrid`)

- TP-level baseline policies applied via runbooks (ADR 0011)
- Per-repo build validation applied during scaffold (`idp:ado-repo-governance`)
- Deploy approvals via ADO Environments (`develop`, `homolog`, `production`)

### 7.4 ADO identity resolution

`adoIdentityResolver.ts` calls ADO Graph API (`vssps.dev.azure.com/{org}/_apis/graph/groups`) to resolve VSTS group identity for governance provisioning. This is ADO Graph, not MS Graph.

### 7.5 Corporate catalog

External catalog repository ingested via URL location:

- Organization/project: configured in `idpCatalog` section
- Repository: `platform-devops-idp-catalog`
- Ingestion: `catalog.locations` URL target to `catalog-info.yaml`

---

## 8. Software Templates (current scope)

Four .NET 10 templates registered in `catalog.locations`:

| Template | Path | Type |
|----------|------|------|
| Minimal API | `templates/dotnet-minimal-api/template.yaml` | `service` |
| Worker Service | `templates/dotnet-worker-service/template.yaml` | `service` |
| gRPC Service | `templates/dotnet-grpc-service/template.yaml` | `service` |
| CronJob | `templates/dotnet-cronjob/template.yaml` | `service` |

### Standard scaffold flow

```text
1. idp:dotnet-project-name        Derive PascalCase project name
2. fetch:template                 Generate code, tests, pipeline, catalog-info, mkdocs
3. publish:azure                  Create ADO repository
4. idp:ado-pipeline-create        Register CI pipeline
5. idp:ado-repo-governance        Apply hybrid governance (build validation)
6. catalog:register               Register in current portal instance
```

Each template uses `TeamProjectPicker` (`ui:field: TeamProjectPicker`) for Team Project selection.

**No change-management or GMUD template exists.**

---

## 9. Catalog metadata for GMUD pre-population

The showcase component (`examples/showcase/component.yaml`) demonstrates annotations and relationships available for future GMUD form pre-population:

| Field | Source | Annotation / spec |
|-------|--------|-------------------|
| Application name | Component metadata | `metadata.name`, `metadata.title` |
| Owner | Catalog spec | `spec.owner` → `group:default/cloud_azure_devops_*` |
| System | Catalog spec | `spec.system` |
| ADO project | Annotation | `azure.devops.com/project` |
| ADO repo | Annotation | `dev.azure.com/project-repo` |
| Build definition | Annotation | `dev.azure.com/build-definition` |
| Source links | Annotations | `backstage.io/view-url`, `backstage.io/edit-url` |
| Requester | Auth session | Authenticated Backstage user → catalog `User` entity |
| Tags, lifecycle, type | Catalog spec | `spec.type`, `spec.lifecycle`, `metadata.tags` |

Reference documentation: `examples/showcase/docs/reference/catalog-annotations.md`

---

## 10. TechDocs, search, and ancillary capabilities

### TechDocs

| Environment | Builder | Publisher |
|-------------|---------|-----------|
| Development | `local` (docker generator) | `local` |
| Production | `external` (docker generator) | `awsS3` |

Build script: `scripts/build-showcase-techdocs.sh` (root `yarn techdocs:showcase`)

### Search

- Engine: PostgreSQL (`@backstage/plugin-search-backend-module-pg`)
- Collators: catalog entities, TechDocs documents

### Other installed capabilities

| Capability | State |
|------------|-------|
| Kubernetes | Plugin installed; `kubernetes:` config section is placeholder |
| Notifications | Backend + frontend UI |
| Signals | Backend + frontend UI |
| MCP actions | Enabled via `mcpActions` config; plugin sources: auth, catalog, scaffolder |
| API docs | Auto-discovered frontend plugin |
| Catalog graph | Auto-discovered frontend plugin |

---

## 11. ADO governance runbooks (operational context)

Location: `docs/runbooks/ado-governance/`

These runbooks provision **deployment approval infrastructure** in Azure DevOps. They are operationally related to the Teams Approval Gateway POC but separate from GMUD creation in Backstage.

### Script sequence

| Step | Script | Purpose |
|------|--------|---------|
| 0 | `00-resolve-ado-identity.sh` | Resolve ADO storage keys |
| 1 | `01-create-role-groups.sh` | Create VSTS role groups |
| 2 | `02-apply-tp-baseline-policies.sh` | Cross-repo branch policies |
| 3 | `05-apply-branch-security.sh` | Branch security |
| 4 | `03-create-environments.sh` | Environments + approval checks |
| 5 | `04-replicate-all-team-projects.sh` | Replicate to all Team Projects |
| 6 | `06-remove-entra-governance-groups.sh` | Remove legacy Entra governance groups |
| 7 | `07-cleanup-legacy-governance.sh` | Cleanup legacy naming artifacts |

### Environments per Team Project

- `develop` — no approval required
- `homolog` — `Homologation Deploy Approvers`
- `production` — `Production Deploy Approvers`

Supporting files: `group-registry.yaml`, `governance-group-names.sh`, `REPLICATION.md`, `PILOT-GARANTIA-STATUS.md`

---

## 12. Backstage ADRs (implementation decisions)

The Backstage repository contains 11 ADRs documenting platform decisions:

| ADR | Title | Relevance to GMUD |
|-----|-------|-------------------|
| 0001 | Entra ID authentication | User identity for "Solicitante" field |
| 0002 | MS Graph org ingestion | Org structure, group sync |
| 0003 | ADO Entra project groups | Team Project ↔ Entra group mapping |
| 0004 | Community RBAC | Permission model for GMUD creation |
| 0005 | Ownership, Systems, RBAC | Application/owner pre-population from catalog |
| 0006 | ado-project-access API | Team Project picker pattern |
| 0007 | IDP catalog register PR | Catalog registration (not GMUD) |
| 0008 | ADO pipeline registration | Pipeline context (not GMUD) |
| 0010 | ADO repo governance | Governance policies (not GMUD) |
| 0011 | Enterprise governance model | ADO approval infrastructure |
| 0012 | Identity catalog RBAC integration | Group taxonomy, governance separation |

No Backstage ADR documents GMUD or change management.

---

## 13. Gap analysis vs. GMUD architecture (bridge repo)

| Bridge expectation | Backstage current state | Gap status |
|--------------------|------------------------|------------|
| ADR-002: Scaffolder-based "Create Production Change" | Only .NET service templates exist | **Not implemented** |
| UI spec: `GMUD` sidebar + "Nova GMUD" form | No nav entry, page, or template | **Not implemented** |
| ADR-003: Change Management API + `IChangeManagementProvider` | No backend module or API | **Not implemented** |
| Pipeline `changeId` correlation | No template variable or scaffolder action | **Not implemented** |
| ADR-003: Canonical change model (`changeId`, risk, window, rollback) | No domain types or storage | **Not implemented** |
| Catalog-derived application/owner fields | Catalog + System/Component entities exist | **Foundation ready** |
| User identity for "Solicitante" | Entra sign-in + catalog User entity | **Foundation ready** |
| ADO project/pipeline context | `ado-project-access` API, showcase annotations | **Partial foundation** |
| RBAC for change creation | `template_executor` role exists for scaffolder | **Reusable pattern** |
| Custom scaffolder form fields | `TeamProjectPicker` demonstrates pattern | **Reusable pattern** |
| Provider adapter (SharePoint/Jira/ServiceNow) | No integration module | **Not implemented** |

---

## 14. Documented conflicts

Per the source-of-truth rule, conflicts between architecture documentation and observed implementation are recorded explicitly. No silent resolution is attempted.

| Topic | Architecture / documentation | Observed implementation | Resolution owner |
|-------|------------------------------|------------------------|------------------|
| GMUD onramp | Bridge ADR-002 assumes Scaffolder MVP for "Create Production Change" | No GMUD scaffolder template or action | Architecture reviewer → implementation decision in ADO repo |
| GMUD nav/UI | `docs/ui/gmud-create-screen.md` defines `GMUD` sidebar entry and "Nova GMUD" page | No frontend route, nav item, or module | Same |
| SharePoint as GMUD store | `docs/future-gmud-context-enrichment.md` assumes SharePoint is GMUD source of truth | Bridge ADR-003 supersedes with provider-agnostic contract | Bridge repo ADRs take precedence |
| Catalog registration default | Backstage ADR-0007: `catalog:register` is default | Showcase README still mentions PR-based registration to `platform-devops-idp-catalog` | Backstage ADR-0007 is authoritative; showcase docs have drift |
| ADR file naming | Runbook README links `0012-integration-model.md` | Actual file: `0012-identity-catalog-rbac-integration.md` | Backstage docs fix (out of scope for this handoff) |
| Sign-in resolver | Backstage ADR-0001 mentions `userIdMatchingUserEntityAnnotation` | Code uses custom `entraUserWithTeamProjectMembership` with TP membership check | Backstage ADR-0001 is outdated; code is authoritative |
| Auth provider packages | `github-provider` and `guest-provider` in backend `package.json` | Not registered in `index.ts`; only Microsoft provider active | Dependency cleanup opportunity (not GMUD-related) |

---

## 15. Integration points for GMUD MVP (observations only)

These are neutral observations for the architecture reviewer. They are **not** implementation decisions.

### 15.1 Scaffolder template path (aligns with bridge ADR-002 MVP)

The bridge ADR-002 recommends a Scaffolder/form-driven GMUD creation flow for MVP. The Backstage repo already has:

- Custom form field pattern (`TeamProjectPicker` with `FormFieldBlueprint`)
- RBAC-gated scaffolder access (`template_executor` role)
- Catalog entity relationships for pre-population

A GMUD template could follow the same patterns without requiring a full custom plugin initially.

### 15.2 Backend Change Management API (required by bridge ADR-003)

No existing module can serve as the Change Management API. A new backend plugin (e.g., `change-management`) would be needed to:

- Expose `IChangeManagementProvider` adapter interface
- Create/read canonical change records
- Return stable `changeId` for pipeline correlation

### 15.3 Catalog context pre-population

Selecting a `Component` or `System` entity could pre-fill GMUD form fields:

- Application name from `metadata.title`
- Owner from `spec.owner` (Entra group display name)
- ADO project from `azure.devops.com/project`
- Repository/pipeline links from `dev.azure.com/*` annotations

### 15.4 Pipeline correlation

Publishing `changeId` to a pipeline requires a new scaffolder output step or post-creation action. No existing action writes arbitrary pipeline variables. The `idp:ado-pipeline-create` action creates pipelines but does not set runtime variables.

### 15.5 Approval flow boundary

Per bridge ADR-002 and ADR-001:

- Backstage is the **creation onramp** for change requests
- Azure DevOps remains the **approval authority**
- Teams Approval Gateway POC (separate repository) handles approval notification and interaction
- Backstage does not need to implement approval UI for MVP

### 15.6 RBAC considerations

A new permission (e.g., `change-management.change.create`) may be needed alongside or instead of reusing `scaffolder.template.execute`. The Community RBAC plugin supports adding new permissions via CSV policy files.

---

## 16. Architecture flow (current state)

```text
                         ┌─────────────────────────────────┐
                         │     Microsoft Entra ID          │
                         │  (OAuth + MS Graph org sync)    │
                         └──────────┬──────────────────────┘
                                    │
                    ┌───────────────┼───────────────┐
                    │               │               │
                    v               v               v
            ┌───────────┐   ┌───────────┐   ┌───────────────┐
            │   Auth    │   │  Catalog  │   │  Permission   │
            │  Plugin   │   │  + MS     │   │  + RBAC       │
            │           │   │  Graph    │   │  (Casbin)     │
            └─────┬─────┘   └─────┬─────┘   └───────┬───────┘
                  │               │                  │
                  v               v                  v
            ┌─────────────────────────────────────────────┐
            │              Backstage Frontend              │
            │  Catalog / Scaffolder / RBAC / TechDocs   │
            │  Custom: nav, auth, TeamProjectPicker,    │
            │          README/Changelog tabs              │
            └──────────────────┬──────────────────────────┘
                               │
              ┌────────────────┼────────────────┐
              │                │                │
              v                v                v
      ┌──────────────┐  ┌──────────────┐  ┌──────────────┐
      │ ado-project- │  │ idp-         │  │ catalog-     │
      │ access API   │  │ provisioner  │  │ entity-files │
      └──────┬───────┘  └──────┬───────┘  └──────────────┘
             │                 │
             v                 v
      ┌──────────────────────────────────┐
      │        Azure DevOps            │
      │  Repos / Pipelines / Policies  │
      │  Environments / Approvals      │
      └──────────────────────────────────┘

      ┌──────────────────────────────────┐
      │   GMUD / Change Management       │
      │   *** NOT IMPLEMENTED ***        │
      └──────────────────────────────────┘
```

---

## 17. Cross-repository information flow

```text
Azure DevOps Backstage repository (platform-devops-developer-portal)
        |
        |  read-only discovery (this document)
        v
GitHub poc-teams-approval / docs/backstage/current-state.md
        |
        |  external architecture review
        v
Implementation decision
        |
        v
Azure DevOps Backstage repository (authoritative for Backstage code)
```

---

## 18. Inspection evidence index

Key files reviewed during discovery:

| Area | Primary evidence files |
|------|------------------------|
| Backend entry | `packages/backend/src/index.ts` |
| Frontend entry | `packages/app/src/App.tsx`, `packages/app/src/index.tsx` |
| Auth | `modules/authMicrosoftEntraModule.ts`, `modules/entraSignInResolvers.ts`, `modules/entraOwnershipResolverModule.ts` |
| RBAC | `packages/backend/config/rbac/rbac-policy.csv`, `packages/backend/config/rbac/conditional-policies.yaml` |
| ADO provisioning | `modules/idpProvisioner/idpProvisionerModule.ts`, `modules/idpProvisioner/*.ts` |
| Project access | `plugins/adoProjectAccessPlugin.ts`, `modules/adoProjectAccess/*.ts` |
| Catalog | `modules/teamProjectGroupDisplayModule.ts`, `modules/staleEntraGovernanceGroupCleanupModule.ts` |
| Templates | `templates/dotnet-*/template.yaml` |
| Showcase | `examples/showcase/component.yaml`, `examples/showcase/catalog-info.yaml` |
| Config | `app-config.yaml`, `app-config.production.yaml`, `.env.example` |
| Versions | `backstage.json`, `package.json`, `packages/backend/package.json`, `packages/app/package.json` |
| ADRs | `docs/adrs/0001` through `0012` |
| Runbooks | `docs/runbooks/ado-governance/` |

---

*End of current-state inventory. This document reflects the Backstage repository as observed on 2026-08-29. Implementation changes after this date are not reflected.*
