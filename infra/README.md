# Infrastructure as Code

This directory captures the Azure baseline already validated manually for the POC.

## Scope

The Bicep currently models:

- resource group `rg-ado-teams-poc`;
- Flex Consumption plan (`FC1`, Linux);
- Azure Function App using .NET 10 isolated;
- 512 MB Flex Consumption instances;
- Storage Account used by the Functions host;
- private deployment package blob container;
- Application Insights;
- system-assigned Managed Identity;
- `Storage Blob Data Owner` on the host Storage Account;
- `Storage Blob Data Contributor` on the deployment package container;
- `Monitoring Metrics Publisher` on Application Insights;
- Azure Bot (`bot-ado-teams-poc-diegolab`) with SingleTenant configuration;
- Microsoft Teams channel on the Azure Bot;
- Function App bot settings: `MicrosoftAppId`, `MicrosoftAppTenantId`, `MicrosoftAppPassword`;
- POC tags;
- public ingress enabled and no VNet integration, matching the current POC posture.

### Intentionally external / manual

- Entra App Registration lifecycle (`5936429a-7889-45c1-983e-d9064aa7ee84`) — referenced by parameters only, not created by Bicep;
- Bot App Registration client secret value — supplied at deploy time via `@secure()` parameter, never committed;
- Web Chat and Direct Line channels — Portal defaults, not managed in this slice;
- Azure Cost Management budget.

## Critical: appSettings replace semantics

`Microsoft.Web/sites` `siteConfig.appSettings` uses **replace semantics**. The Bicep template declares the complete authoritative list of application settings. Any manually-added setting not represented in Bicep will be **deleted** on the next deployment.

The template currently owns these Function App settings:

- `AzureWebJobsStorage__accountName`
- `AzureWebJobsStorage__credential`
- `APPLICATIONINSIGHTS_CONNECTION_STRING`
- `APPLICATIONINSIGHTS_AUTHENTICATION_STRING`
- `MicrosoftAppId`
- `MicrosoftAppTenantId`
- `MicrosoftAppPassword`

`APPINSIGHTS_INSTRUMENTATIONKEY` is intentionally omitted — it is redundant with `APPLICATIONINSIGHTS_CONNECTION_STRING`.

Do **not** add `FUNCTIONS_WORKER_RUNTIME` or `FUNCTIONS_EXTENSION_VERSION` on Flex Consumption. Runtime is configured via `functionAppConfig.runtime`.

## Important: do not deploy first

These resources already exist because they were created manually in the Azure Portal.

The first operation must be a `what-if`, not a deployment. The purpose is to compare the desired Bicep model against the resources provisioned by the portal and identify differences before IaC takes ownership of configuration.

## Prerequisites

Install Azure CLI and Bicep, then authenticate to the Diegolab tenant.

```bash
az login
az account show --output table
```

Make sure the selected subscription is the POC subscription before continuing.

If needed:

```bash
az account set --subscription "Azure subscription 1"
```

## Compile validation

Run from the repository root:

```bash
mkdir -p build/infra
az bicep build --file infra/main.bicep --outfile build/infra/main.json
```

Generated ARM JSON belongs under `build/` (gitignored), never beside `infra/main.bicep`. This must succeed before any Azure operation.

## What-if

Because `main.bicep` is subscription-scoped and `microsoftAppPassword` is a secure parameter, supply it via environment variable at the command line. Never commit the secret.

```bash
read -s MICROSOFT_APP_PASSWORD && export MICROSOFT_APP_PASSWORD
az deployment sub what-if \
  --name poc-teams-approval-bot-iac-check \
  --location eastus2 \
  --template-file infra/main.bicep \
  --parameters infra/poc.bicepparam
unset MICROSOFT_APP_PASSWORD
```

`poc.bicepparam` reads the secret with `readEnvironmentVariable('MICROSOFT_APP_PASSWORD')` — no value is stored in the repository.

Review every proposed change.

Expected goal:

```text
No destructive changes.
No Function App replacement.
No Storage Account replacement.
No Managed Identity replacement.
No unexpected networking change.
No RBAC removal.
No Create for existing Azure Bot or MsTeamsChannel.
No Delete for WebChatChannel or DirectLineChannel.
No removal of APPLICATIONINSIGHTS_CONNECTION_STRING or APPLICATIONINSIGHTS_AUTHENTICATION_STRING.
MicrosoftAppPassword preserved (not null, not removed).
```

Acceptable changes:

- Modify on existing Azure Bot and MsTeamsChannel (adoption);
- Modify on Function App appSettings (bot settings adoption, removal of redundant APPINSIGHTS_INSTRUMENTATIONKEY);
- ARM expression/default-value normalization noise on platform resources.

Stop and investigate if what-if shows Create for the bot, Create for MsTeamsChannel, any Delete, Function replacement, identity change, WebChat/DirectLine channel changes, or MicrosoftAppPassword removal.

## Deployment

Do not run this until the `what-if` has been reviewed and accepted:

```bash
read -s MICROSOFT_APP_PASSWORD && export MICROSOFT_APP_PASSWORD
az deployment sub create \
  --name poc-teams-approval-bot-iac-adopt \
  --location eastus2 \
  --template-file infra/main.bicep \
  --parameters infra/poc.bicepparam
unset MICROSOFT_APP_PASSWORD
```

## Current POC networking warning

The Bicep intentionally matches the current POC:

```text
Function public access: Enabled
VNet integration: Disabled
```

This is not the intended production security posture. The production design will be documented separately and must evaluate controlled public ingress plus private Function/network integration.

## Files

```text
infra/
├── main.bicep
├── poc.bicepparam
├── README.md
└── modules/
    ├── platform.bicep
    └── bot.bicep
```
