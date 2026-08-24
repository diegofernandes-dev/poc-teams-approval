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
- POC tags;
- public ingress enabled and no VNet integration, matching the current POC posture.

The Azure Cost Management budget is intentionally not included in this first IaC slice.

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
az bicep build --file infra/main.bicep
```

This must succeed before any Azure operation.

## What-if

Because `main.bicep` is subscription-scoped:

```bash
az deployment sub what-if \
  --name poc-teams-approval-iac-check \
  --location eastus2 \
  --template-file infra/main.bicep \
  --parameters infra/poc.bicepparam
```

Review every proposed change.

Expected goal:

```text
No destructive changes.
No Function App replacement.
No Storage Account replacement.
No Managed Identity replacement.
No unexpected networking change.
No RBAC removal.
```

Some differences may exist because the Azure Portal creates defaults that were not explicitly selected during the walkthrough. Those differences must be reviewed individually and either represented in Bicep or intentionally accepted before deployment.

## Deployment

Do not run this until the `what-if` has been reviewed and accepted:

```bash
az deployment sub create \
  --name poc-teams-approval-infra \
  --location eastus2 \
  --template-file infra/main.bicep \
  --parameters infra/poc.bicepparam
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
    └── platform.bicep
```
