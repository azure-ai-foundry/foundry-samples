# Investment Planner — Foundry Skills + Agent Identity

A **prompt agent** on the Foundry managed harness that produces a 6‑month investment plan.
The sample demonstrates two things together:

1. **Foundry Skills** — reusable, versioned `SKILL.md` + scripts the agent loads on demand.
2. **Agent (managed) identity** — the agent's *own* identity (not the user's) reaches
   **Key Vault**, the user's **Blob Storage**, and the project **Files API** from inside the
   harness sandbox, so protected resources are accessed **without any key/SAS/credential ever
   entering the model's context**.

The primary driver is the notebook [`investment_planner.ipynb`](./investment_planner.ipynb);
[`main.py`](./main.py) is a thin "run it all" script that does the same create → run → clean‑up.

## Scenario

The user drops a holdings CSV in **their own blob container** (same tenant). The agent reads
that blob **and** pulls the user's financial profile from a credential‑protected API — both via
its own identity — analyzes the portfolio with plain Python **inside the harness sandbox**,
renders a `six_month_plan.pdf` (via `reportlab`), and uploads it back to the project. Every
identity‑bound I/O is done by a skill under the agent identity; the analysis and PDF rendering
run on the same sandbox disk, so the downloaded CSV, the computed plan, and the uploaded file
all share one filesystem.

## The four skills

Sources live under [`skills/`](./skills); `provision_skills.py` publishes them to your
project and attaches them to a toolbox it creates (`investment-skills`), fronted by an
agent-identity (`AgenticIdentityToken`) project connection it also creates.

| Skill | Role | Identity used |
|---|---|---|
| [`financial-profile`](./skills/financial-profile/SKILL.md) | Read Key Vault `sig` → GET profile API → return **only** profile JSON | agent identity → Key Vault |
| [`blob-reader`](./skills/blob-reader/SKILL.md) | Download the holdings CSV from the user's blob (AAD, no key/SAS) | agent identity → Blob Storage |
| [`keyvault-secret-reader`](./skills/keyvault-secret-reader/SKILL.md) | Generic: read any named secret (contrast skill) | agent identity → Key Vault |
| [`calling-project-file-api`](./skills/calling-project-file-api/SKILL.md) | Upload the finished plan to the project | agent identity → Files API |

**The composed‑vs‑generic contrast is the lesson.** `financial-profile` composes the Key
Vault read *inside its own process* and returns only the profile, so the raw credential never
surfaces. `keyvault-secret-reader` *can* return the raw value — prefer the composed pattern
whenever the secret is only a means to an end.

## Prerequisites

- An Azure AI Foundry **project** with a deployed model. Need one? Deploy
  [`infrastructure-setup-bicep/40-basic-agent-setup`](../../../../infrastructure/infrastructure-setup-bicep/40-basic-agent-setup).
- **Azure CLI** logged in (`az login`).
- Python 3.10+ and a package manager (`uv` or `pip` + `venv`).

### How the agent identity works (read this first)

Every hosted agent runs as its **own Microsoft Entra service principal — the *agent
identity*** — created by the platform **when you create the agent**. Skill scripts in the
sandbox authenticate as that identity through `DefaultAzureCredential`, so the agent identity
is what reaches Key Vault, Blob Storage, and the project Files API **at runtime**, no matter
where you invoke the agent from. (See
[Hosted agents, part 3](https://ankitbko.github.io/blog/2026/05/hosted-agents-part-3/).)

Two consequences shape the order below:

1. **The agent identity does not exist until the agent is created**, so its RBAC role
   assignments must happen **after** `create`, not before.
2. Your **dev identity** (`az login`) is used only to *author* — publish skills, create the
   agent, set the Key Vault secret, and upload the blob. It is **not** what the skills use at
   runtime.

| Phase | Identity | Roles |
|---|---|---|
| Author (before/at create) | you (`az login`) | rights to set a KV secret + upload a blob (e.g. Owner, or **Key Vault Secrets Officer** + **Storage Blob Data Contributor**), and **Azure AI User** on the project to publish skills / create the agent |
| Runtime (granted **after** create) | the **agent identity** | **Key Vault Secrets User** (`4633458b-17de-408a-b874-0445c86b69e6`) on the vault · **Storage Blob Data Reader** (`2a2b9908-6ea1-4ae2-8e65-a410df84e7d1`) on the storage account · **Azure AI User** on the project (Files API) |

### Author-time Azure setup

Store **only** the profile API's shared‑access signature (`sig`) in Key Vault — the base URL
is non‑secret config that goes in `.env`:

```bash
az keyvault secret set --vault-name <vault> --name financial-profile-sig --value "<the-sig>"
```

Upload the sample [`data/holdings.csv`](./data/holdings.csv) to a container in **your own**
storage account (same tenant):

```bash
az storage blob upload \
  --account-name <account> --container-name <container> \
  --name holdings.csv --file ./data/holdings.csv --auth-mode login
```

Put the resulting blob URL in `HOLDINGS_BLOB_URL`
(`https://<account>.blob.core.windows.net/<container>/holdings.csv`).

> The **runtime** role assignments (to the agent identity) come later, in step 3 of **Run** —
> after the agent exists.

### The profile API (Logic App)

The sample expects a GET endpoint returning this fixed schema:

```json
{
  "risk_tolerance": "moderate",
  "investable_cash_usd": 50000,
  "horizon_months": 6,
  "goals": ["retirement"],
  "current_holdings": [{ "ticker": "MSFT", "qty": 40 }],
  "constraints": { "no_crypto": true }
}
```

A Consumption **Logic App** with an *HTTP request* trigger → *Response* action returning this
body works well. Put the invoke URL **without** the `&sig=...` query part in
`FINANCIAL_PROFILE_URL`; the `sig` itself goes in Key Vault (above).

## Configure

```bash
cp .env.sample .env
# then fill in the values
```

| Variable | Meaning |
|---|---|
| `AZURE_AI_PROJECT_ENDPOINT` | `https://<res>.services.ai.azure.com/api/projects/<project>` |
| `AZURE_AI_MODEL_DEPLOYMENT_NAME` | e.g. `gpt-5.4` |
| `KEYVAULT_URL` | `https://<vault>.vault.azure.net` |
| `FINANCIAL_PROFILE_URL` | Logic App invoke URL **without** `&sig=...` |
| `HOLDINGS_BLOB_URL` | `https://<account>.blob.core.windows.net/<container>/holdings.csv` |
| `PROJECT_RESOURCE_ID` | ARM id of the Foundry project — used to derive the toolbox connection id and to grant the agent identity `Azure AI User` |
| `KEYVAULT_RESOURCE_ID` / `STORAGE_RESOURCE_ID` | scopes for the post-create RBAC grants |

> **No toolbox or connection variables.** The sample owns its tool surface:
> `provision_skills.py` creates a toolbox (`investment-skills`) and an agent-identity
> (`AgenticIdentityToken`) project connection (`investment-skills-toolbox`) that fronts its MCP
> endpoint. The connection id is
> derived from `PROJECT_RESOURCE_ID`, so there is nothing to copy back into `.env`.

## Install

```bash
# uv
uv sync
uv run --group notebook jupyter lab   # or run main.py below

# pip
python3 -m venv .venv
./.venv/bin/pip install -r requirements.txt
```

## Run

The order matters: the agent identity only exists after **create**, so its roles are granted
between **create** and **run**.

### 1. Publish the skills, toolbox, and connection (once, and after any skill edit)

```bash
python provision_skills.py
```

This packages every file under each `skills/<name>/` folder (including `scripts/`) and
publishes each skill via the data‑plane Skills REST API. It then **creates the toolbox**
(`investment-skills`) and attaches all four skills as its default version, and **creates the
Entra project connection** (`investment-skills-toolbox`) whose `target` is the toolbox's
MCP endpoint. Because the toolbox is in the same project, the connection uses the
`AgenticIdentityToken` auth type — the running agent's own identity token authorizes the MCP
call (its `Azure AI User` role), so no key or SAS is stored. Requires `PROJECT_RESOURCE_ID` in `.env`.

### 2. Create the agent

```bash
python main.py create
```

This creates the agent version and prints its **agent identity principal id** (fetched via
`az rest ... instance_identity.principal_id`).

### 3. Grant the agent identity its runtime roles

```bash
# Print ready-to-paste az commands (fills in the principal id for you):
python main.py grant

# ...or set KEYVAULT_RESOURCE_ID / STORAGE_RESOURCE_ID / PROJECT_RESOURCE_ID in .env and:
python main.py grant --apply
```

This assigns **Key Vault Secrets User**, **Storage Blob Data Reader**, and **Azure AI User**
to the agent identity (`--assignee-principal-type ServicePrincipal`). Allow ~1–5 minutes for
RBAC to propagate before running.

### 4. Run the agent

- **Notebook (recommended):** open [`investment_planner.ipynb`](./investment_planner.ipynb)
  and run the cells top to bottom — it walks through the same create → grant → run order.
- **Script:** `python main.py run`  (then `python main.py delete` to clean up)

## Managed‑harness note

The **agent‑identity** story is real at runtime **regardless of where you invoke from**:
skills execute inside the harness sandbox, where the platform delivers the *agent's* token to
`DefaultAzureCredential`. Your local `az login` identity is used only to author (publish
skills, create the agent, set the secret, upload the blob) — it is **not** what reads Key
Vault or Blob Storage when the agent runs. That is exactly why the roles in step 3 are granted
to the **agent identity's** service principal, not to you.

## Limitations

- Analysis and PDF rendering run as plain Python **on the harness sandbox disk** — not the
  built‑in code interpreter tool, which is a *separate* sandbox and would not see the
  skill‑downloaded CSV/profile. Keeping them on one filesystem is what lets the produce step
  (render `six_month_plan.pdf`) and the upload step share the same file. Large artifacts are
  exchanged via the Files API using the `calling-project-file-api` skill.
- `reportlab` is installed on demand in the sandbox (turn 1); the plan is emitted as
  `six_month_plan.pdf` and uploaded with content type `application/pdf`.
- The financial‑profile schema is fixed (see above); adjust the skill and instructions if your
  profile API differs.

---

> This is a generated example and **not financial advice**.
