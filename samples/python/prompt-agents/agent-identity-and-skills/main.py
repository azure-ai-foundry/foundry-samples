"""Investment-planner prompt agent — Foundry Skills + agent identity (correct RBAC order).

Every hosted agent runs as its **own** Entra service principal (the *agent identity*),
created by the platform when the agent is created. Skill scripts in the sandbox authenticate
as that identity via ``DefaultAzureCredential`` — so the agent's identity is what reaches Key
Vault, Blob Storage, and the project Files API at runtime (your dev identity is only used
locally to *author*: publish skills and create the agent).

Because the agent identity does not exist until the agent is created, RBAC must be granted
*after* creation. This script is therefore split into ordered subcommands:

    python main.py create   # create the agent version, then print its identity's principal id
    python main.py grant     # print (or --apply) the az role assignments for that identity
    python main.py run       # invoke the agent (needs the roles above, propagated)
    python main.py delete    # remove the agent version(s)

Run ``provision_skills.py`` once before ``create`` to publish + attach the skills.
See README.md for the full walkthrough.
"""
import argparse
import os
import sys
import time

import dotenv
from azure.ai.projects import AIProjectClient
from azure.ai.projects.models import CodeInterpreterTool, MCPTool, PromptAgentDefinition
from azure.identity import DefaultAzureCredential

from sample_config import AGENT_NAME, TOOLBOX_CONNECTION_NAME, run_az, toolbox_mcp_url

dotenv.load_dotenv()

# Built-in role definition ids for the agent-identity grants.
ROLE_KV_SECRETS_USER = "4633458b-17de-408a-b874-0445c86b69e6"
ROLE_STORAGE_BLOB_DATA_READER = "2a2b9908-6ea1-4ae2-8e65-a410df84e7d1"

INSTRUCTIONS = """\
You are an investment-planning assistant. Produce a 6-month allocation plan for the user's
portfolio. Follow this procedure and use the attached skills — do not invent data:

1. Call the `financial-profile` skill to obtain the user's profile JSON (risk_tolerance,
   investable_cash_usd, horizon_months, goals, current_holdings, constraints). The skill
   reads its API credential from Key Vault using your managed identity; never ask the user
   for it and never print the credential.
2. Use the `blob-reader` skill to download the holdings CSV from the user's Azure Blob
   Storage (env HOLDINGS_BLOB_URL) using your managed identity — do not ask for a key or SAS.
   Parse it in the code interpreter (columns: ticker, name, sector, qty, avg_cost_usd,
   current_price_usd, dividend_yield_pct, beta, analyst_rating) and compute current position
   values and portfolio weights.
3. Build a 6-month allocation that respects the profile's risk_tolerance, investable_cash_usd,
   goals, and constraints (e.g. honor `no_crypto`). Show target weights, specific buy/trim
   actions for the investable cash, and a one-line rationale per action.
4. Write the plan to `investment-plan.md` and upload it to the project using the
   `calling-project-file-api` skill; report the returned file id.
5. End with this exact disclaimer on its own line:
   "This is a generated example and not financial advice."
"""


def _client():
    endpoint = os.environ["AZURE_AI_PROJECT_ENDPOINT"].rstrip("/")
    return endpoint, AIProjectClient(endpoint=endpoint, credential=DefaultAzureCredential())


def _agent_principal_id(endpoint):
    """Fetch the agent identity's Entra object id (only available after the agent exists)."""
    out = run_az(
        ["rest", "--method", "GET",
         "--url", f"{endpoint}/agents/{AGENT_NAME}?api-version=v1",
         "--resource", "https://ai.azure.com",
         "--query", "instance_identity.principal_id", "-o", "tsv"],
    )
    pid = out.stdout.strip()
    if out.returncode != 0 or not pid:
        print("WARNING: could not read instance_identity.principal_id "
              f"(az rest said: {out.stderr.strip()}).", file=sys.stderr)
        return None
    return pid


def cmd_create(args):
    endpoint, client = _client()
    # The MCP tool points at the toolbox's data-plane MCP endpoint; auth is supplied by the
    # project connection (AgenticIdentityToken) referenced by name. `harness="ghcp"` runs the
    # agent in the managed harness so skills execute server-side under the agent identity.
    server_url = f"{toolbox_mcp_url(endpoint)}?api-version=v1"
    definition = PromptAgentDefinition(
        model=os.environ["AZURE_AI_MODEL_DEPLOYMENT_NAME"],
        instructions=INSTRUCTIONS,
        temperature=0,
        tools=[
            CodeInterpreterTool(),
            MCPTool(
                server_url=server_url,
                server_label="toolbox",
                require_approval="never",
                project_connection_id=TOOLBOX_CONNECTION_NAME,
            ),
        ],
    )
    definition["harness"] = "ghcp"
    with client:
        agent = client.agents.create_version(
            agent_name=AGENT_NAME,
            definition=definition,
        )
        print(f"Agent created (id={agent.id}, name={agent.name}, version={agent.version})")
        print(f"Harness: ghcp | Toolbox MCP: {server_url}")
        print(f"Toolbox connection: {TOOLBOX_CONNECTION_NAME}")
    pid = _agent_principal_id(endpoint)
    if pid:
        print(f"Agent identity (principal id): {pid}")
        print("Next: run `python main.py grant` to assign its Key Vault / Blob / project roles.")


def cmd_grant(args):
    endpoint, _ = _client()
    pid = _agent_principal_id(endpoint)
    if not pid:
        sys.exit("Create the agent first (`python main.py create`).")

    kv_scope = os.environ.get("KEYVAULT_RESOURCE_ID", "<key-vault-resource-id>")
    blob_scope = os.environ.get("STORAGE_RESOURCE_ID", "<storage-account-resource-id>")
    project_scope = os.environ.get("PROJECT_RESOURCE_ID", "<foundry-project-resource-id>")

    grants = [
        ("Key Vault Secrets User", kv_scope),
        ("Storage Blob Data Reader", blob_scope),
        # Files API is a project data-plane call; Azure AI User covers it.
        ("Azure AI User", project_scope),
    ]

    for name, scope in grants:
        az_args = [
            "role", "assignment", "create",
            "--assignee-object-id", pid,
            "--assignee-principal-type", "ServicePrincipal",
            "--role", name,
            "--scope", scope,
        ]
        if args.apply and "<" not in scope:
            print(f"Assigning '{name}' to {pid} on {scope} ...")
            r = run_az(az_args)
            print(r.stdout or r.stderr)
        else:
            print(f"# {name}\naz " + " ".join(az_args) + "\n")

    if not args.apply:
        print("Set KEYVAULT_RESOURCE_ID / STORAGE_RESOURCE_ID / PROJECT_RESOURCE_ID and re-run "
              "with --apply, or paste the commands above. Allow ~1-5 min for RBAC to propagate.")


PROMPT = (
    "Pull my financial profile, read my holdings CSV from my blob storage, analyze "
    "the portfolio, and produce my 6-month investment plan. Upload it to the project."
)


def _stream_response(openai_client, conversation_id, prompt):
    """Create a streaming response and print output/tool/reasoning events as they arrive."""
    print(f"Sending prompt to {AGENT_NAME}...")
    print("=" * 80)
    print()

    all_events = []
    response_id = None
    terminal = False
    start = time.time()
    with openai_client.responses.with_streaming_response.create(
        conversation=conversation_id,
        model=os.environ["AZURE_AI_MODEL_DEPLOYMENT_NAME"],
        input=prompt,
        stream=True,
        extra_body={"agent_reference": {"type": "agent_reference", "name": AGENT_NAME}},
    ) as api_response:
        session_id = api_response.headers.get("x-agent-session-id")
        print(f"HTTP status: {api_response.status_code}")
        print(f"x-request-id: {api_response.headers.get('x-request-id')}")
        print(f"x-agent-session-id: {session_id}")
        print()
        print("-" * 80)
        print()

        for event in api_response.parse():
            all_events.append(event.type)

            if event.type == "response.created":
                response_id = event.response.id
                print(f"\U0001f680 Response ID: {response_id}\n")
            elif event.type == "response.output_text.delta":
                print(event.delta, end="", flush=True)
            elif event.type == "response.output_item.added":
                item = getattr(event, "item", None)
                if item is not None:
                    if item.type == "function_call":
                        print(f"\n\n\U0001f527 Tool call: {item.name}")
                    elif item.type == "reasoning":
                        print("\n\U0001f9e0 Reasoning...")
                    else:
                        print(f"\n\U0001f4e6 Output item: {item.type}")
            elif event.type == "response.reasoning.delta":
                if hasattr(event, "delta"):
                    print(event.delta, end="", flush=True)
            elif event.type == "response.function_call_arguments.done":
                if hasattr(event, "arguments"):
                    print(f"\n    args: {event.arguments[:200]}")
            elif event.type == "response.output_item.done":
                item = getattr(event, "item", None)
                if item is not None and item.type == "function_call":
                    print(f"    \u2705 Tool call done: {item.name}")
                elif item is not None and item.type == "function_call_output":
                    print(f"    \U0001f4cb Tool output: {getattr(item, 'output', '')[:300]}")
            elif event.type == "response.output_text.done":
                print()
            elif event.type == "response.completed":
                terminal = True
                print("\n\n\u2705 Response completed")
            elif event.type == "response.failed":
                terminal = True
                print("\n\n\u274c Response FAILED")
                err = getattr(getattr(event, "response", None), "error", None)
                if err is not None:
                    print(f"    Error: {err}")
            elif event.type == "response.incomplete":
                terminal = True
                print("\n\n\u26a0\ufe0f Response incomplete")
            elif event.type == "response.reasoning_summary_text.delta":
                if hasattr(event, "delta"):
                    print(event.delta, end="", flush=True)
            elif event.type in (
                "response.reasoning_summary_part.added",
                "response.reasoning_summary_text.done",
                "response.reasoning_summary_part.done",
                "response.in_progress",
                "response.content_part.added",
                "response.content_part.done",
                "response.function_call_arguments.delta",
            ):
                pass

    # The ghcp harness runs asynchronously and may close the SSE stream after only
    # `created`/`in_progress`, finishing in the background. Poll until terminal in that case.
    if response_id and not terminal:
        _poll_until_terminal(openai_client, response_id)

    elapsed = time.time() - start
    print(f"\n{'=' * 80}")
    print(f"Total time: {elapsed:.1f}s | events: {len(all_events)}")
    print(f"Event types: {sorted(set(all_events))}")


def _poll_until_terminal(openai_client, response_id, interval=3, timeout=600):
    """Poll a background (harness) response until it reaches a terminal state, then print it."""
    print("\n\u23f3 Stream closed early; polling the background response ...")
    deadline = time.time() + timeout
    while time.time() < deadline:
        resp = openai_client.responses.retrieve(response_id)
        if resp.status in ("completed", "failed", "incomplete", "cancelled"):
            if resp.status == "completed":
                print("\n\u2705 Response completed\n")
                print(resp.output_text)
            elif resp.status == "failed":
                print(f"\n\u274c Response FAILED: {getattr(resp, 'error', None)}")
            else:
                print(f"\n\u26a0\ufe0f Response {resp.status}")
            return resp
        time.sleep(interval)
    print("\n\u26a0\ufe0f Timed out waiting for the background response to finish.")
    return None
    print(f"\n{'=' * 80}")
    print(f"Total time: {elapsed:.1f}s | events: {len(all_events)}")
    print(f"Event types: {sorted(set(all_events))}")


def cmd_run(args):
    _, client = _client()
    with client:
        openai_client = client.get_openai_client()
        # A response must be created within a conversation.
        conversation = openai_client.conversations.create()
        print(f"Conversation created: {conversation.id}")
        _stream_response(openai_client, conversation.id, PROMPT)


def cmd_delete(args):
    _, client = _client()
    with client:
        versions = list(client.agents.list_versions(agent_name=AGENT_NAME))
        for v in versions:
            client.agents.delete_version(agent_name=AGENT_NAME, agent_version=v.version)
        print(f"Deleted {len(versions)} version(s) of '{AGENT_NAME}'.")


def main():
    p = argparse.ArgumentParser(description=__doc__)
    sub = p.add_subparsers(dest="cmd", required=True)
    sub.add_parser("create").set_defaults(func=cmd_create)
    g = sub.add_parser("grant")
    g.add_argument("--apply", action="store_true",
                   help="actually run the az role assignments (needs *_RESOURCE_ID env vars)")
    g.set_defaults(func=cmd_grant)
    sub.add_parser("run").set_defaults(func=cmd_run)
    sub.add_parser("delete").set_defaults(func=cmd_delete)
    args = p.parse_args()
    args.func(args)


if __name__ == "__main__":
    main()
