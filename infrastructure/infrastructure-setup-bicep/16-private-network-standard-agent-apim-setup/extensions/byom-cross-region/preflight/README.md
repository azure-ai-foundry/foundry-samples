# Pre-flight region check

`check-region.ps1` verifies that the region pair you intend to use for this extension will actually deploy successfully — before you start a 30+ minute deployment that could fail on quota, model availability, or APIM SKU support in minute 28.

It runs the same checks documented inline in the [parent README's *Preflight checks* section](../README.md#preflight-checks), but as a single PowerShell command that also (a) auto-ranks alternative regions when your chosen pair fails, and (b) prints the exact `az deployment group create` command to run on success.

> **You don't need this script to deploy.** It's a convenience helper. The inline `az` one-liners in the parent README cover the same gates if you prefer not to run a PowerShell script.

## Why this exists

The extension provisions resources across two regions, and several failure modes only surface late in the bicep run:

* `gpt-5` or `gpt-5.1` not in the backend region's model catalogue → fails after the APIM + private endpoint are already provisioned.
* `OpenAI.GlobalStandard.*` quota exhausted → fails per-deployment, mid-rollout.
* APIM StandardV2 SKU not supported in the project region → fails ~10 minutes in.
* Required RP not registered → fails on the first resource of that kind.

This script catches all four in under 90 seconds against an unused subscription, or under 30 seconds if all providers are already cached.

## What it checks

Per region, in this order:

| # | Check | Source | Blocks deploy? |
|---|---|---|---|
| 0 | All required ARM providers are `Registered` on the sub | `az provider show` | **Yes** |
| 1 | Region is in template-16 `main.bicep` `@allowed` list (project region only) | hardcoded — keep in sync with [`../../main.bicep`](../../main.bicep) lines 6–32 | **Yes** — bicep compile error |
| 2 | `Microsoft.CognitiveServices/accounts` lists this region | `az provider show` | **Yes** |
| 3 | `Microsoft.ApiManagement/service` lists this region (project only) | `az provider show` | **Yes** |
| 4 | Region is in the **APIM StandardV2 SKU** list (project only) | hardcoded — see [`aka.ms/apim-v2-tiers`](https://aka.ms/apim-v2-tiers) | **Yes** — APIM provisioning fails |
| 5 | `Microsoft.Search/searchServices` lists this region (project only) | `az provider show` | **Yes** unless `-BringYourOwnSearch` |
| 6 | `Microsoft.Storage/storageAccounts` lists this region (project only) | `az provider show` | **Yes** unless `-BringYourOwnStorage` |
| 7 | `Microsoft.DocumentDB/databaseAccounts` lists this region (project only) | `az provider show` | **Yes** unless `-BringYourOwnCosmos` |
| 8 | Every required `(model, version, SKU)` tuple is in the regional model catalogue | `az cognitiveservices model list` | **Yes** — model deployment fails |
| 9 | Each model's OpenAI quota family has enough free TPM (`limit - currentValue >= required`) | `az cognitiveservices usage list` | **Yes** — quota exceeded |

Non-blocking warnings:

* **Storage no-ZRS regions** (`southindia`, `westus`, `canadaeast`) — template falls back to `Standard_GRS` instead of `Standard_ZRS`. No action needed.
* **Cosmos canary regions** (`eastus2euap`, `centraluseuap`) — template auto-rewrites the Cosmos location to `westus`.

## Requirements

* **PowerShell 7+**
* **Azure CLI 2.67+** (`az login` must have run; the script switches `az` context to the subscription you pass).
* Read access on the subscription (`az cognitiveservices` and `az provider` are both read-only).

## Quickstart

```powershell
cd extensions/byom-cross-region/preflight

# Non-interactive: verify a known pair
.\check-region.ps1 -Subscription <sub-id> -ProjectRegion canadaeast -BackendRegion japaneast

# Auto-pick the best regions for both sides
.\check-region.ps1 -Subscription <sub-id> -ProjectRegion auto -BackendRegion auto

# Interactive: prompt for both
.\check-region.ps1 -Subscription <sub-id>

# Survey every region that would work today
.\check-region.ps1 -Subscription <sub-id> -ListAvailable

# Reuse an existing Cosmos / Search / Storage account
.\check-region.ps1 -Subscription <sub-id> -ProjectRegion canadaeast -BackendRegion japaneast `
  -BringYourOwnCosmos -BringYourOwnSearch -BringYourOwnStorage
```

## Matching the script to your `main.bicepparam`

The script ships with defaults that match the bicepparam shipped with this extension (`gpt-4o` in the project region; `gpt-4o`, `gpt-5`, `gpt-5.1` in the backend region; `GlobalStandard` SKU; 30k TPM project / 10k TPM backend). If you change the bicepparam, mirror the change on the script so the preflight verifies what you'll actually deploy.

| What you changed in `main.bicepparam` | What to pass to the script |
|---|---|
| `location = '<region>'` | `-ProjectRegion <region>` |
| `backendLocation = '<region>'` | `-BackendRegion <region>` |
| `projectModelName`, `projectModelVersion` | `-ProjectModels '<name>@<version>'` |
| `projectModelSkuName` | `-ModelSku <sku>` |
| `projectModelCapacity` | `-ProjectModelCapacity <k-TPM>` |
| Added/removed entries in `backendModelDeployments` | `-BackendModels '<name>@<version>',...` |
| `backendModelDeployments[*].skuName` | `-ModelSku <sku>` *(applies to all entries — keep them uniform or run separate checks)* |
| `backendModelDeployments[*].capacity` | `-BackendModelCapacity <k-TPM>` *(applies to all entries)* |
| Any of `aiSearchResourceId`, `azureStorageAccountResourceId`, `azureCosmosDBAccountResourceId` set | `-BringYourOwnSearch` / `-BringYourOwnStorage` / `-BringYourOwnCosmos` |

### Model-list syntax

The `-ProjectModels` and `-BackendModels` parameters accept comma-separated entries in one of two forms:

| Form | Meaning |
|---|---|
| `<name>@<version>` | Pinned — checks the exact `(name, version, SKU)` tuple in the regional catalogue. **Use this when your bicepparam pins a version.** |
| `<name>` | Unpinned — checks that *some* version of `<name>` is available at the chosen SKU, and lists the available versions in the output. Use this for exploratory checks when you haven't decided on a version yet. |

Mix them in a single call:

```powershell
.\check-region.ps1 -Subscription <sub-id> `
  -ProjectRegion eastus -BackendRegion swedencentral `
  -BackendModels 'gpt-4o@2024-11-20','gpt-4.1','gpt-4o-mini@2024-07-18'
```

### Worked examples

**1. Smaller / cheaper deployment** — only `gpt-4o` on the backend, half the default capacity:

```powershell
.\check-region.ps1 -Subscription <sub-id> `
  -ProjectRegion eastus -BackendRegion eastus2 `
  -BackendModels 'gpt-4o@2024-11-20' `
  -BackendModelCapacity 5
```

**2. Brand-new model family** — verify availability without pinning a version (handy when a model is just released and you want the latest):

```powershell
.\check-region.ps1 -Subscription <sub-id> `
  -ProjectRegion swedencentral -BackendRegion swedencentral `
  -ProjectModels 'gpt-4o' `
  -BackendModels 'gpt-5','gpt-5.1'
```

**3. Mixed pinned + unpinned** — pin the model that's in production, leave the experimental one open:

```powershell
.\check-region.ps1 -Subscription <sub-id> `
  -ProjectRegion canadaeast -BackendRegion japaneast `
  -BackendModels 'gpt-4o@2024-11-20','gpt-5.1'
```

**4. Different SKU** — provisioned-managed throughput instead of pay-as-you-go:

```powershell
.\check-region.ps1 -Subscription <sub-id> `
  -ProjectRegion eastus -BackendRegion eastus2 `
  -ModelSku 'ProvisionedManaged' `
  -BackendModels 'gpt-4o'
```

> When you change `-ModelSku`, every entry in `-ProjectModels` and `-BackendModels` is checked against that single SKU. If your bicepparam mixes SKUs across models, run the script once per SKU with the matching subset of models.

### Hardcoded values you should never need to change

These are properties of the template or of Azure itself — not customer choices — so they live as constants inside the script:

* `$script:TemplateAllowedRegions` — must match the `@allowed` list at the top of [`../../main.bicep`](../../main.bicep) (template 16). If the template adds a region, add it here too.
* `$script:ApimStandardV2Regions` — Azure's APIM StandardV2 regional availability. Refresh from [`aka.ms/apim-v2-tiers`](https://aka.ms/apim-v2-tiers) periodically.
* `$script:CosmosCanaryRegions`, `$script:StorageNoZrsRegions` — Azure region-behaviour quirks the bicep already handles.

## Parameters

| Parameter | Default | Meaning |
|---|---|---|
| `-Subscription` | **required** | Subscription id to check. Script switches `az` context to it. |
| `-ProjectRegion` | `prompt` | Region for Foundry project + APIM. `prompt` asks, `auto` picks best, otherwise a region name. |
| `-BackendRegion` | `prompt` | Region for the backend Foundry account. Same special values. |
| `-ProjectModels` | `'gpt-4o@2024-11-20'` | Comma-separated list of models required in the project region. Each entry is `<name>` or `<name>@<version>`. |
| `-BackendModels` | `'gpt-4o@2024-11-20','gpt-5@2025-08-07','gpt-5.1@2025-11-13'` | Same format. Lists models required in the backend region. |
| `-ModelSku` | `GlobalStandard` | Cognitive Services SKU applied to every model in the lists above. |
| `-ProjectModelCapacity` | `30` | TPM (k) required for EACH project-region model. Matches `projectModelCapacity` in `main.bicepparam`. |
| `-BackendModelCapacity` | `10` | TPM (k) required for EACH backend-region model. Matches `backendModelDeployments[*].capacity`. |
| `-MaxAlternatives` | `5` | How many alternative regions to recommend on failure. |
| `-NoSuggest` | off | Skip the alternative-region scan on failure. |
| `-ListAvailable` | off | Skip chosen-region check; just enumerate every region that would work. |
| `-BringYourOwnSearch` | off | Skip the AI Search region check (reusing `aiSearchResourceId`). |
| `-BringYourOwnStorage` | off | Skip the Storage region check (reusing `azureStorageAccountResourceId`). |
| `-BringYourOwnCosmos` | off | Skip the Cosmos region check (reusing `azureCosmosDBAccountResourceId`). |

## Exit codes

| Code | Meaning |
|---|---|
| `0` | Both regions pass. Script prints the `az deployment group create` command to run. |
| `1` | At least one region failed a check, OR a required RP isn't registered. Alternatives (if any) are in the summary. |
| `2` | `auto` mode couldn't find any region that satisfies the requirements (extremely rare). |

## Sample output (happy path, `canadaeast` / `japaneast`)

```
=================================================================
 Pre-flight region check — Foundry + APIM + cross-region backend
=================================================================
 Subscription : Contoso Production (a71e73e0-…)
 Identity     : alice@contoso.com [user]

--- Resource provider registration (subscription-wide) ---------------
    [ OK ]  Microsoft.CognitiveServices                        Registered
    [ OK ]  Microsoft.ApiManagement                            Registered
    [ OK ]  Microsoft.Search                                   Registered
    [ OK ]  Microsoft.Storage                                  Registered
    [ OK ]  Microsoft.DocumentDB                               Registered
    [ OK ]  Microsoft.Network                                  Registered
    …

--- Verifying PROJECT region: canadaeast -----------------------------
    [ OK ]  Region in template @allowed list                   Accepted by main.bicep
    [ OK ]  CognitiveServices/accounts in canadaeast           Region listed by ARM provider
    [ OK ]  ApiManagement/service in canadaeast                Region listed by ARM provider
    [ OK ]  APIM StandardV2 SKU in canadaeast                  SV2 supported
    [ OK ]  Search/searchServices in canadaeast                Region listed by ARM provider
    [WARN]  Storage/storageAccounts in canadaeast              Region has no ZRS — bicep will deploy Standard_GRS instead
    [ OK ]  DocumentDB/databaseAccounts in canadaeast          Region listed by ARM provider
    [ OK ]  gpt-4o@2024-11-20 [GlobalStandard]                 Available in catalogue
    [ OK ]    quota: OpenAI.GlobalStandard.gpt-4o              free 390 / limit 450 >= requested 30 (k TPM)

--- Verifying BACKEND region: japaneast ------------------------------
    [ OK ]  CognitiveServices/accounts in japaneast            Region listed by ARM provider
    [ OK ]  gpt-4o@2024-11-20 [GlobalStandard]                 Available in catalogue
    [ OK ]    quota: OpenAI.GlobalStandard.gpt-4o              free 450 / limit 450 >= requested 10 (k TPM)
    [ OK ]  gpt-5@2025-08-07 [GlobalStandard]                  Available in catalogue
    [ OK ]    quota: OpenAI.GlobalStandard.gpt-5               free 1000 / limit 1000 >= requested 10 (k TPM)
    [ OK ]  gpt-5.1@2025-11-13 [GlobalStandard]                Available in catalogue
    [ OK ]    quota: OpenAI.GlobalStandard.gpt-5.1             free 1000 / limit 1000 >= requested 10 (k TPM)

=================================================================
 Summary
=================================================================
 Project (Foundry+APIM) canadaeast      PASS
 Backend (models)     japaneast       PASS

 Both regions are good. Deploy with:

   cd ..
   az group create --name <rg> --location canadaeast
   az deployment group create `
     --resource-group <rg> `
     --template-file main.bicep `
     --parameters '@samples/parameters-cross-region.json' `
     --parameters location=canadaeast backendLocation=japaneast `
                  projectMiClientId=<paste-client-id>
```

## Sample output (failure with alternatives)

```
--- Verifying PROJECT region: westus2 --------------------------------
    [FAIL]  Region in template @allowed list                   Not in main.bicep @allowed (would fail bicep compile)

--- Project region 'westus2' won't work — finding alternatives -------

  Scanning 23 candidate regions for Project (Foundry + APIM)...
    [1/23] australiaeast ... OK (free=450)
    …

   Try one of these instead (ranked by free TPM):
     australiaeast           free TPM (sum)= 450
     southindia              free TPM (sum)= 450
     polandcentral           free TPM (sum)= 450
```

