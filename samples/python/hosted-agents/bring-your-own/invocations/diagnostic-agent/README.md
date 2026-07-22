<!-- Begin standard disclaimer — do not modify -->
**IMPORTANT!** All samples and other resources made available in this GitHub repository ("samples") are designed to assist in accelerating development of agents, solutions, and agent workflows for various scenarios. Review all provided resources and carefully test output behavior in the context of your use case. AI responses may be inaccurate and AI actions should be monitored with human oversight. Learn more in the transparency note for [Agent Service](https://learn.microsoft.com/en-us/azure/ai-foundry/responsible-ai/agents/transparency-note).

Agents, solutions, or other output you create may be subject to legal and regulatory requirements, may require licenses, or may not be suitable for all industries, scenarios, or use cases. By using any sample, you are acknowledging that any output created using those samples are solely your responsibility, and that you will comply with all applicable laws, regulations, and relevant safety standards, terms of service, and codes of conduct.

Third-party samples contained in this folder are subject to their own designated terms, and they have not been tested or verified by Microsoft or its affiliates.

Microsoft has no responsibility to you or others with respect to any of these samples or any resulting output.
<!-- End standard disclaimer -->

# Diagnostic Agent (Python, Invocations)

A **diagnostic** hosted-agent built on the Invocations protocol. It does **not** call an LLM and does **not** require a Foundry project endpoint or a model deployment. Instead, on each invocation, it runs DNS / TCP / TLS / HTTP probes against caller-supplied hostnames and returns a structured JSON report describing what the runtime sandbox can actually reach.

Use this image to answer questions like:

- From inside the delegated `agent-subnet-*`, what does `<customer>.azurecr.io` resolve to? A private IP or a public one?
- Does `https://<customer>.azurecr.io/v2/` return `401 Unauthorized` (registry reachable) or does the request hang / get connection refused / TLS-verify-fail?
- Can the runtime egress to public Azure endpoints (`login.microsoftonline.com`, `management.azure.com`) or only to private endpoints?

## Design notes

- **Stdlib-only probe code.** All DNS / TCP / TLS / HTTP probes are written against `socket`, `ssl`, `urllib`, and `http.client`. The network is the very thing being diagnosed; the probes must not depend on import-time package fetches or pyca handshakes that obscure the failure mode.
- **No model, no project endpoint.** The manifest declares no `resources` and no `environment_variables`. The image is portable across any Foundry project.
- **Single JSON response.** All probe outcomes are returned in one HTTP 200 response — per-probe failures are reported in the `status` / `hint` fields, not via non-2xx HTTP codes. This keeps client-side parsing simple.
- **Caller controls the probe matrix.** The request body lists hostnames; nothing is hard-coded to a specific customer ACR. An empty body runs only the safe defaults (container info, env dump, and a small set of public Azure endpoints).
- **No secrets in the response.** Env vars matching `KEY`, `SECRET`, `PASSWORD`, `TOKEN`, `CONNECTION_STRING`, or `SAS` are reported with their length only.

## Getting Started (Bring Your Own Infrastructure)

This sample is designed for **Bring Your Own** (BYO) infrastructure scenarios where the Azure Foundry account, project, and supporting resources are already provisioned separately.

### Prerequisites

- An existing **Azure Foundry project** (account + project already created)
- An existing **container registry** (if deploying in container mode)
- **Azure CLI** with `azure.ai.agents` extension installed:
  ```bash
  azd config set ai.agents.version 0.1.22-preview
  ```

### Deployment

1. **Set environment variables** — Copy `.env.example` to `.env` and fill in your existing Foundry project details:
   ```bash
   cp .env.example .env
   ```

   Edit `.env` with your project information:
   ```env
   AZURE_AI_PROJECT_ID=/subscriptions/<sub-id>/resourceGroups/<rg>/providers/Microsoft.CognitiveServices/accounts/<account>/projects/<project>
   AZURE_AI_PROJECT_ENDPOINT=https://<account>.services.ai.azure.com/api/projects/<project>
   AZURE_SUBSCRIPTION_ID=<sub-id>
   AZURE_ENV_NAME=<env-name>
   AZURE_LOCATION=<region>
   AZURE_CONTAINER_REGISTRY_ENDPOINT=<registry>.azurecr.io  # For container mode only
   ```

2. **Deploy the agent** — Use `azd deploy` (not `azd up`, since no infrastructure needs provisioning):

   **Option A: Container Mode (Recommended)** — Docker image pushed to container registry:
   ```bash
   # Default configuration — uses azure.yaml as-is
   azd deploy --no-prompt
   ```
   - Builds Docker image from `Dockerfile`
   - Pushes to `AZURE_CONTAINER_REGISTRY_ENDPOINT`
   - Deploys container to Foundry
   - **Requires**: ACR configured in `.env`

   **Option B: ZIP Mode** — Bundle Python code directly (no container):
   ```bash
   # Step 1: Edit azure.yaml
   # Change this line:
   #   language: docker
   # To:
   #   language: python
   #
   # And remove the docker section entirely (lines 12-13):
   #   docker:
   #       remoteBuild: false

   # Step 2: Deploy
   azd deploy --no-prompt
   ```
   - Bundles Python source code as ZIP
   - Deploys to Foundry without container image
   - **No ACR required**
   - Useful for code-only scenarios or testing

3. **Invoke the agent** — Once deployed, invoke it via REST:
   ```bash
   TOKEN=$(az account get-access-token --resource https://ai.azure.com --query accessToken -o tsv)
   curl -X POST \
     "https://<account>.services.ai.azure.com/api/projects/<project>/agents/diagnostic-agent-python-invocations/endpoint/protocols/invocations?api-version=v1" \
     -H "Authorization: Bearer $TOKEN" \
     -H "Content-Type: application/json" \
     -d '{"hosts": ["microsoft.com"]}'
   ```

## Deployment Modes Comparison

| Aspect | Container Mode | ZIP Mode |
|--------|---|---|
| **Command** | `azd deploy --no-prompt` (default) | Edit `azure.yaml`, then `azd deploy --no-prompt` |
| **Build Process** | Builds Docker image → Pushes to ACR | Bundles Python code as ZIP |
| **Requires ACR** | ✅ Yes | ❌ No |
| **Container Image Size** | ~500 MB (python:3.12-slim) | N/A (code only) |
| **Startup Speed** | ~30 seconds | ~30 seconds (similar) |
| **Use Case** | Production, versioned images | Testing, code-only scenarios |
| **Config Change** | None (default) | Edit `azure.yaml` (1 line) |

Validation: both execution paths were tested locally after removing IMDS/MSI support.

## Troubleshooting: ACR Not Reachable From Private Network

If private networking is misconfigured, container-mode deployment can fail before the diagnostic image is even available (for example, DNS failure, blocked egress, or Private Endpoint routing issues to your private ACR).

Use one of the following fallback paths to keep debugging network reachability:

### Path 1 (Preferred): ZIP deploy from a VM attached to the target network

This route avoids ACR entirely and still runs the same diagnostics code in Foundry.

1. Use a VM that is attached to the same VNet/subnet path you want to validate.
2. In `azure.yaml`, switch to ZIP mode:
  - Change `language: docker` to `language: python`.
  - Remove the `docker:` block.
3. Deploy with:
  ```bash
  azd deploy --no-prompt
  ```
4. Invoke the agent and probe your private endpoints as usual.

When ZIP mode works but container mode fails, the issue is typically on the ACR path (DNS, NSG/UDR/firewall, or PE routing), not in the probe logic.

### Path 2: Temporary public-ACR fallback for image distribution

If you must validate container mode while private ACR is unreachable, use a temporary public ACR (PNA enabled) for this diagnostic image.

1. Set `AZURE_CONTAINER_REGISTRY_ENDPOINT=<public-registry>.azurecr.io` in `.env`.
2. Run:
  ```bash
  azd deploy --no-prompt
  ```
3. Re-run the same diagnostic probes.

If deployment succeeds with public ACR but fails with private ACR, the regression is isolated to private ACR connectivity/policy.

For security, treat this as a short-lived troubleshooting step only: remove temporary public exposure and revert to private ACR once networking is fixed.

## Request body contract

All fields are optional:

```json
{
  "hosts": [
    "<customer-acr>.azurecr.io",
    "<customer-acr>.<region>.data.azurecr.io"
  ],
  "public_hosts": [
    "https://www.microsoft.com/",
    "https://management.azure.com/metadata/endpoints?api-version=2020-09-01",
    "https://login.microsoftonline.com/common/v2.0/.well-known/openid-configuration"
  ],
  "resolvers":      ["168.63.129.16"],
  "record_types":   ["A", "AAAA"],
  "raw_dns":        true,
  "direct_targets": ["10.0.1.9:443"],
  "include_env_dump":       true,
  "include_container_info": true,
  "tcp_timeout_sec":  5,
  "http_timeout_sec": 10,
  "dns_timeout_sec":  5
}
```

| Field | Default | Notes |
|---|---|---|
| `hosts` | `[]` | List of FQDNs. For each, runs raw DNS (dig) + DNS → TCP/443 → TLS/443 → HTTPS GET. For `*.azurecr.io` and `*.data.azurecr.io` hosts, the GET path is `/v2/` (returns 401 with `Www-Authenticate` when reachable). For all other hosts, GET path is `/`. |
| `public_hosts` | small built-in list | Full URLs. HTTPS-GET only — no DNS/TCP/TLS breakdown. Pass `[]` to skip. |
| `resolvers` | *(system)* | **Extra** DNS servers to query alongside the ones in `/etc/resolv.conf`. Add `168.63.129.16` (or the platform default) to expose a private-zone linkage gap as a per-resolver disagreement. |
| `record_types` | `["A","AAAA"]` | Record types queried per resolver by the raw DNS client. |
| `raw_dns` | `true` | Automate `dig <type> @<resolver>` for every (resolver × record type): reports the real rcode (SERVFAIL/REFUSED/NXDOMAIN/NODATA/timeout), the CNAME chain, latency, and cross-resolver disagreement. Runs **even when `getaddrinfo` fails** (the EAI_AGAIN case). |
| `dns_attempts` | `1` | Repeat each raw DNS query N times to expose **intermittency**. Reports per-resolver `timeout_rate`, `successes/attempts`, and min/max/avg latency; classifies `DNS_INTERMITTENT` / `DNS_OK_PRIVATE_INTERMITTENT` when some attempts answer and others time out. On any UDP timeout it also probes **TCP/53** and flags `DNS_UDP_DROP_TCP_OK` (EDNS/MTU fragmentation). |
| `gai_attempts` | `1` | Repeat the OS `getaddrinfo` call N times and report the **failure rate** (`successes/failures`, per-error counts) — measures the intermittent `EAI_AGAIN` the app actually experiences, separate from the raw wire-level result. |
| `parallel_probe` | `false` | Mimic glibc's default `getaddrinfo`: send **A and AAAA back-to-back on one UDP socket** and measure how often a reply is lost (`both_ok_rate`). A loss here while raw sequential queries are clean is the signature of a **concurrent-query** problem (`PARALLEL_DUAL_LOSS`) — the cause of `getaddrinfo` failures that `dig` cannot reproduce and that firewalls show no drops for. |
| `direct_targets` | `[]` | `ip:port` (or `host:port`) reachability tests that **skip DNS** — isolate a network-path break from a DNS break. |
| `include_env_dump` | `true` | Returns env vars matching an allowlist prefix (`FOUNDRY_`, `AZURE_`, `KUBERNETES_`, etc.); credential-shaped values are length-only. |
| `include_container_info` | `true` | Hostname, container IP, default gateway from `/proc/net/route`, resolvers + full `resolv.conf` detail (`search`, `ndots`, `timeout`, `attempts`). |
| `tcp_timeout_sec` | `5` | Per-attempt TCP/TLS timeout. |
| `http_timeout_sec` | `10` | HTTP timeout. |
| `dns_timeout_sec` | `5` | Per-query raw DNS timeout. |

You may also send a **plain-text body** containing a single hostname; the agent treats it as `{"hosts": ["<text>"]}`. Useful from the Foundry portal chat UI.

If the body is empty, the agent runs only the defaults: container info + env dump + the built-in public-host list. No private hosts are probed unless explicitly requested.

## Response shape

```json
{
  "status": "ok",
  "agent_session_id": "...",
  "invocation_id": "...",
  "timestamp_utc": "2026-06-12T...",
  "checks": {
    "container": {
      "hostname": "...",
      "ip": "10.0.0.42",
      "default_route": "10.0.0.1 via eth0",
      "resolvers": ["168.63.129.16"]
    },
    "env": {
      "AZURE_REGION": "westus2",
      "FOUNDRY_PROJECT_ENDPOINT": "https://...",
      "KUBERNETES_SERVICE_HOST": "10.0.0.1"
    },
    "hosts": [
      {
        "host": "<acr>.azurecr.io",
        "dns":      {"status": "ok", "ips": ["10.0.1.4"], "any_private": true, "all_private": true},
        "dns_raw": {
          "per_resolver": [
            {"resolver": "10.0.0.10", "classification": "DNS_OK_PRIVATE",
             "cname_chain": ["<acr>.privatelink.azurecr.io"],
             "records": {"A": {"rcode": "NOERROR", "answers": ["10.0.1.4"], "ms": 6.2}},
             "dig": ";; ->>HEADER<<- status: NOERROR, 2 answer(s), 6.2ms udp\n..."},
            {"resolver": "168.63.129.16", "classification": "DNS_OK_PUBLIC_FOR_PRIVATE",
             "records": {"A": {"rcode": "NOERROR", "answers": ["20.53.44.203"]}}, "hint": "..."}
          ],
          "resolver_disagreement": {"classification": "RESOLVER_DISAGREE",
            "private_from": ["10.0.0.10"], "not_private_from": ["168.63.129.16"], "hint": "..."}
        },
        "dns_vs_raw": {"classification": "GAI_RAW_CONSISTENT", "getaddrinfo_ips": ["10.0.1.4"], "raw_ips": ["10.0.1.4"]},
        "tcp_443":  {"status": "ok", "ip": "10.0.1.4", "port": 443, "ms": 1.8},
        "tls_443":  {"status": "ok", "version": "TLSv1.3", "cipher": "TLS_AES_256_GCM_SHA384", "cert_subject": "CN=*.azurecr.io", "cert_sans": ["*.azurecr.io", "*.<region>.data.azurecr.io"]},
        "http_get": {"status": "ok", "code": 401, "headers": {"www-authenticate": "Bearer realm=...", "docker-distribution-api-version": "registry/2.0"}}
      }
    ],
    "public_hosts": [
      {"status": "ok", "url": "https://www.microsoft.com/", "code": 200}
      ]
  }
}
```

## Interpretation cheat-sheet

| Symptom in response | Likely cause |
|---|---|
| `hosts[].dns.status = FAIL gaierror` | Resolver doesn't have the zone. For `privatelink.*`, the private DNS zone isn't linked to this VNet. **See `hosts[].dns_raw` for the real rcode.** |
| `hosts[].dns.ips` all RFC1918 → ✅ | Private Endpoint resolution is working. |
| `hosts[].dns.ips` contain a public IP for a `privatelink.*` host | Zone link missing or pointed at the wrong VNet. `hint` field flags this. |
| `dns_raw.per_resolver[].classification = DNS_OK_PUBLIC_FOR_PRIVATE` | Name CNAMEs to `privatelink.*` but resolved to a **public** IP. For a **configured** resolver this means it doesn't serve the private zone; for an **extra** comparison resolver (e.g. `168.63.129.16` from a spoke VNet) this is **expected** in a centralized/hub-DNS design. |
| `dns_raw.per_resolver[].classification = DNS_INTERMITTENT` / `DNS_OK_PRIVATE_INTERMITTENT` | Some attempts answered, others timed out (`timeout_rate`). The record exists but delivery through this resolver/path is unreliable (packet loss, forwarder capacity, flaky hub/ExpressRoute hop). Fix DNS-path reliability; add a second resolver or a local Private Resolver. |
| `dns_raw.per_resolver[].classification = DNS_UDP_DROP_TCP_OK` | UDP/53 times out but TCP/53 answers — EDNS/MTU fragmentation dropped on the path. Allow UDP fragments/EDNS(0) or use a local Private Resolver. |
| `dns_raw.verdict` | The classification of the **configured** resolver(s) only — use this as the authoritative verdict; extra comparison resolvers are informational. |
| `dns_raw.resolver_disagreement = RESOLVER_DISAGREE` | Some resolvers return the private IP, some don't — the private zone is only linked to part of the DNS path. Classic "works from the VM subnet, not the agent subnet." |
| `dns_raw.per_resolver[].classification = DNS_SERVFAIL` | Resolver/forwarder is authoritative-but-broken for the zone (or its conditional forwarder failed). Fast failure. |
| `dns_raw.per_resolver[].classification = DNS_REFUSED` | Source-based ACL/view excludes this subnet — authorize the agent-subnet source on the DNS server. |
| `dns_raw.per_resolver[].classification = DNS_TIMEOUT` | Resolver/forwarder unreachable or dropping packets from this subnet (NSG/UDR/peering). Slow failure ⇒ path problem, not a missing record. |
| `dns_raw.per_resolver[].classification = DNS_NODATA` | Name exists (NOERROR) but no A/AAAA record in the zone this resolver serves. |
| `dns_vs_raw.classification = GAI_FAIL_RAW_OK` | `getaddrinfo` fails but the record resolves at the wire level — a dual-stack/AAAA, `ndots`/search-domain, or `nsswitch` quirk, not a DNS outage. |
| `direct_targets[].tcp.status = ok` while the same host fails DNS | The private IP is reachable — the break is **DNS**, not the network path. |
| `tcp_443.status = FAIL timeout` | NSG egress rule, UDR routing to an NVA that black-holes the flow, or firewall drop. |
| `tcp_443.status = FAIL refused` | PE is in Disconnected state, or an upstream device is sending RST. |
| `tls_443.status = FAIL SSLCertVerificationError` | A firewall is doing TLS interception. Bypass `*.azurecr.io` / `*.azure.com`. |
| `tls_443.status = FAIL SSLError` mid-handshake | NVA breaking SNI. Enable SNI passthrough. |
| `http_get.code = 401` on `/v2/` for ACR | Registry is reachable. ✅ |
| `http_get.code = 403` on `/v2/` for ACR | PNA=Disabled + caller not on an approved PE. |

## Per-service expected results

When probing a private-link-enabled Foundry project's BYO dependency
services, each service has a distinct healthy fingerprint. Anything that
deviates from the row below points at a misconfiguration, not auth.

| Service | FQDN pattern | Expected cert SANs | Expected unauth `GET /` |
|---|---|---|---|
| ACR (registry) | `<acr>.azurecr.io` | `*.azurecr.io`, `*.<region>.geo.azurecr.io` | `401` + `WWW-Authenticate: Bearer realm=".../oauth2/token"` (path: `/v2/`) |
| ACR (data) | `<acr>.<region>.data.azurecr.io` | `*.<region>.data.azurecr.io`, `*.azurecr.io`, `*.data.azurecr.io` | `403 DENIED` (path: `/v2/`) |
| Cosmos DB | `<acct>.documents.azure.com` | `*.{sql,mongo,table,gremlin,cassandra}.cosmosdb.azure.com` | `401 Unauthorized` + JSON body about missing `authorization` header |
| Storage (blob) | `<acct>.blob.core.windows.net` | `*.blob.core.windows.net`, `*.blob.storage.azure.net` | `400 InvalidQueryParameterValue` (root GET is malformed by design) |
| AI Search | `<svc>.search.windows.net` | `*.search.windows.net`, `*.management.search.windows.net` | `401 Unauthorized` + `WWW-Authenticate: Bearer ... resource="https://search.azure.com"` |
| AI Services (cognitive) | `<acct>.cognitiveservices.azure.com` | `*.cognitiveservices.azure.com`, `*.openai.azure.com`, `*.services.ai.azure.com` | `200 Service Operational` |
| AI Services (openai) | `<acct>.openai.azure.com` | (same as above) | `200 Service Operational` |
| AI Services (services.ai) | `<acct>.services.ai.azure.com` | `<acct>.services.ai.azure.com` (account-specific cert) | `200 OK` (`server: Kestrel`) |

Any cert issuer other than a `Microsoft TLS …` / `Microsoft Azure RSA TLS Issuing CA …` / per-account cert (for example, an enterprise TLS inspection CA) suggests the TLS handshake was intercepted by a network device instead of terminating at the expected Private Endpoint.

## Realistic multi-service response

Here's what a successful probe against all of a Foundry project's BYO
private endpoints looks like (truncated for readability; placeholder
resource names). DNS resolves to the PE subnet (`192.168.1.0/24`), TCP/443
succeeds, TLS terminates with a Microsoft-issued cert whose SANs cover the
host, and the unauth `GET /` returns each service's expected challenge.

Request:

```json
{
  "hosts": [
    "myorgacr.southindia.data.azurecr.io",
    "myorgacr.azurecr.io",
    "myorgcosmos.documents.azure.com",
    "myorgstorage.blob.core.windows.net",
    "myorgsearch.search.windows.net",
    "myorgaisvc.cognitiveservices.azure.com",
    "myorgaisvc.openai.azure.com",
    "myorgaisvc.services.ai.azure.com"
  ]
}
```

Response (per-host summary, full JSON elided):

| Host | DNS IP | TCP | TLS | HTTP |
|---|---|---|---|---|
| `myorgacr.southindia.data.azurecr.io` | `192.168.1.11` | ok | ok | `403 DENIED` (`/v2/`) |
| `myorgacr.azurecr.io` | `192.168.1.12` | ok | ok | `401 Bearer realm=...` (`/v2/`) |
| `myorgcosmos.documents.azure.com` | `192.168.1.4` | ok | ok | `401` (Cosmos) |
| `myorgstorage.blob.core.windows.net` | `192.168.1.9` | ok | ok | `400 InvalidQueryParameterValue` |
| `myorgsearch.search.windows.net` | `192.168.1.10` | ok | ok | `401` + Search WWW-Authenticate |
| `myorgaisvc.cognitiveservices.azure.com` | `192.168.1.6` | ok | ok | `200 Service Operational` |
| `myorgaisvc.openai.azure.com` | `192.168.1.7` | ok | ok | `200 Service Operational` |
| `myorgaisvc.services.ai.azure.com` | `192.168.1.8` | ok | ok | `200 OK` (Kestrel) |

## Running locally

This sample follows the same `azd ai agent` workflow as the other invocations samples. See [hello-world/README.md](../hello-world/README.md) for the full `azd` / Foundry Toolkit walkthrough.

For the local-only path (no `azd`):

```bash
python -m venv .venv
source .venv/bin/activate
pip install -r requirements.txt
python main.py
```

The agent listens on `http://localhost:8088/`. Invoke it:

```bash
# Default profile (container + env + public hosts only)
curl -sS -X POST "http://localhost:8088/invocations?agent_session_id=diag-001" \
  -H "Content-Type: application/json" -d '{}' | jq

# Probe a specific ACR (registry + data plane)
curl -sS -X POST "http://localhost:8088/invocations?agent_session_id=diag-001" \
  -H "Content-Type: application/json" \
  -d '{
        "hosts": [
          "<acr>.azurecr.io",
          "<acr>.<region>.data.azurecr.io"
        ],
        "public_hosts": []
      }' | jq

# Plain-text body — quick single-host check from the portal chat UI
curl -sS -X POST "http://localhost:8088/invocations" \
  -H "Content-Type: text/plain" \
  --data "<acr>.azurecr.io" | jq
```

The interesting runs happen when the image is deployed into a Foundry project and invoked from there.

## Deploying to Microsoft Foundry

Same `azd` / Foundry Toolkit workflow as the other invocations samples — see [hello-world/README.md](../hello-world/README.md#deploying-the-agent-to-microsoft-foundry). Because the manifest declares no `resources` block, deployment does not provision a model.

## Security notes

- This image is intended for diagnostics, not for production agent traffic. Treat its responses as semi-public: nothing in the response is a credential, but env-var names can reveal infrastructure topology.
- The image never writes secrets. It does not parse, log, or return `Authorization` headers, or any env var matching credential-shaped substrings.
- The image performs HTTPS-GET only. No POST/PUT/DELETE; no authenticated calls to the probed hosts.
