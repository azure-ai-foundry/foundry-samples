#!/usr/bin/env python3
"""
BYO Model via APIM AI Gateway - Agents v2 Test Script

This script tests the cross-region private bring-your-own-model (BYOM) pattern
that template 16 (private-network standard agent + APIM AI Gateway) enables via
its `extensions/byom-cross-region` deployment.

Scenario under test:

    Foundry project (project region)
        -> BYOM model connection  (<connection>/<deployment>)
        -> Azure API Management "AI Gateway"  (/inference API, managed-identity)
        -> cross-region private endpoint
        -> backend Foundry account (second region, publicNetworkAccess=Disabled)

Tests:
    1. APIM Gateway Inference (Direct HTTP) - Direct REST call to the APIM
       /inference endpoint (best-effort; requires a token the gateway accepts).
    2. Agent via BYOM Model - Create an agent whose `model` is the BYOM
       connection reference "<connection>/<deployment>" and run a prompt. This
       exercises the full project -> APIM -> cross-region backend path.
    3. Basic Agent (project-region fallback model) - Sanity check that the
       project endpoint and a local (project-region) deployment work.

Uses the Agents v2 SDK pattern:
    - AIProjectClient with a context manager
    - project_client.get_openai_client() for the OpenAI-compatible API
    - openai_client.responses.create() for the Responses API
    - project_client.agents.create_version() with PromptAgentDefinition

Note: When the Foundry account has public network access disabled (the default
for this template), SDK calls must originate from inside the VNet (VPN /
ExpressRoute / Bastion jump box). See TESTING-GUIDE.md.
"""

import os
import sys
import json
import ssl
import logging
import argparse
import urllib.request
import urllib.error

# ============================================================================
# LOGGING CONFIGURATION
# ============================================================================
LOG_LEVEL = logging.INFO

logging.basicConfig(
    level=LOG_LEVEL,
    format="%(asctime)s - %(name)s - %(levelname)s - %(message)s",
)

logging.getLogger("azure.core.pipeline.policies.http_logging_policy").setLevel(LOG_LEVEL)
logging.getLogger("httpx").setLevel(LOG_LEVEL)
logging.getLogger("urllib3").setLevel(logging.WARNING)
logging.getLogger("azure.identity").setLevel(logging.WARNING)

# ============================================================================

from azure.ai.projects import AIProjectClient
from azure.ai.projects.models import PromptAgentDefinition
from azure.identity import DefaultAzureCredential

# ============================================================================
# CONFIGURATION
# ============================================================================
# Project-scoped endpoint from the Azure Portal:
#   AI Services resource -> Projects -> <project> -> Properties -> "AI Foundry API" endpoint
PROJECT_ENDPOINT = os.environ.get(
    "PROJECT_ENDPOINT",
    "https://aiservices.services.ai.azure.com/api/projects/project",
)

# BYOM connection created by the byom-cross-region extension.
# In agent code the backend model is referenced as "<connection>/<deployment>".
# Defaults mirror the extension's parameters (connectionName='ai-gateway',
# backend deployments gpt-4o / gpt-5 / gpt-5.1).
BYOM_CONNECTION_NAME = os.environ.get("BYOM_CONNECTION_NAME", "ai-gateway")
BYOM_DEPLOYMENT_NAME = os.environ.get("BYOM_DEPLOYMENT_NAME", "gpt-5")

# Model reference used by the agent for the BYOM path.
BYOM_MODEL = os.environ.get(
    "BYOM_MODEL",
    f"{BYOM_CONNECTION_NAME}/{BYOM_DEPLOYMENT_NAME}",
)

# Project-region local model deployment (sanity check / fallback).
FALLBACK_MODEL_NAME = os.environ.get("FALLBACK_MODEL_NAME", "gpt-4o")

# APIM AI Gateway base URL, e.g. https://<apim-name>.azure-api.net
# (the `apimGatewayUrl` output of the byom-cross-region deployment).
APIM_GATEWAY_URL = os.environ.get("APIM_GATEWAY_URL", "")

# Inference API path + version configured on APIM (extension defaults).
INFERENCE_API_PATH = os.environ.get("INFERENCE_API_PATH", "inference")
INFERENCE_API_VERSION = os.environ.get("INFERENCE_API_VERSION", "2024-10-21")

# AAD scope for Cognitive Services / Foundry inference.
COGNITIVE_SCOPE = "https://cognitiveservices.azure.com/.default"

# ============================================================================


def log_response_info(response, label="Response"):
    """Extract and log useful debugging info from OpenAI response objects."""
    logger = logging.getLogger(__name__)
    try:
        if hasattr(response, "_request_id"):
            logger.info(f"{label} - Request ID: {response._request_id}")
        if hasattr(response, "id"):
            logger.info(f"{label} - Response ID: {response.id}")
        if hasattr(response, "_response") and hasattr(response._response, "headers"):
            headers = response._response.headers
            if "x-request-id" in headers:
                logger.info(f"{label} - x-request-id: {headers['x-request-id']}")
            if "x-ms-request-id" in headers:
                logger.info(f"{label} - x-ms-request-id: {headers['x-ms-request-id']}")
    except Exception as e:
        logger.debug(f"Could not extract response info: {e}")


def log_exception_info(exception, label="Exception"):
    """Extract and log request info from OpenAI exceptions."""
    logger = logging.getLogger(__name__)
    try:
        if hasattr(exception, "response") and exception.response is not None:
            resp = exception.response
            headers = resp.headers if hasattr(resp, "headers") else {}

            request_id = headers.get("x-request-id", "N/A")
            ms_request_id = headers.get("x-ms-request-id", "N/A")

            logger.error(f"{label} - x-request-id: {request_id}")
            logger.error(f"{label} - x-ms-request-id: {ms_request_id}")

            print(f"  📋 Request ID (x-request-id): {request_id}")
            print(f"  📋 MS Request ID (x-ms-request-id): {ms_request_id}")

            if hasattr(resp, "status_code"):
                logger.error(f"{label} - HTTP Status: {resp.status_code}")

        if hasattr(exception, "request_id"):
            logger.error(f"{label} - request_id attribute: {exception.request_id}")
            print(f"  📋 Request ID: {exception.request_id}")
    except Exception as e:
        logger.debug(f"Could not extract exception info: {e}")


def test_apim_gateway_inference(
    gateway_url: str,
    deployment_name: str,
    label: str = "APIM Gateway",
):
    """
    Direct HTTP call to the APIM /inference endpoint (best-effort).

    NOTE: This test can only PASS when it runs as the **project managed
    identity** (the identity APIM's `validate-azure-ad-token` policy is
    configured to accept via `<client-application-ids>`). From a developer
    machine, DefaultAzureCredential presents the developer's own token, which
    the gateway rejects (401/403) - so this test reports SKIPPED there, not
    failed. To actually exercise this path, run it from inside the agent
    runtime / a context signed in as the project MI. The authoritative
    end-to-end validation is the agent test (`--test byom`).

    Full URL:
      {gateway}/{path}/deployments/{deployment}/chat/completions?api-version=...

    The APIM `/inference` API validates an Azure AD bearer token (the project
    managed identity's token in production) and then rewrites the request onto
    the cross-region backend Foundry account using its own managed identity.
    """
    print("\n" + "=" * 60)
    print(f"TEST: APIM Gateway Inference (Direct HTTP) - {label}")
    print("=" * 60)

    if not gateway_url:
        print("  ⚠ APIM_GATEWAY_URL not configured, skipping direct gateway test")
        return None

    url = (
        f"{gateway_url.rstrip('/')}/{INFERENCE_API_PATH.strip('/')}"
        f"/deployments/{deployment_name}/chat/completions"
        f"?api-version={INFERENCE_API_VERSION}"
    )
    print(f"  Target: {url}")

    try:
        credential = DefaultAzureCredential()
        token = credential.get_token(COGNITIVE_SCOPE).token

        payload = json.dumps(
            {
                "messages": [
                    {"role": "system", "content": "You are a helpful assistant."},
                    {"role": "user", "content": "Reply with the single word: pong."},
                ],
                "max_tokens": 16,
                "temperature": 0,
            }
        ).encode("utf-8")

        req = urllib.request.Request(
            url,
            data=payload,
            headers={
                "Content-Type": "application/json",
                "Authorization": f"Bearer {token}",
            },
            method="POST",
        )

        ctx = ssl.create_default_context()
        with urllib.request.urlopen(req, timeout=30, context=ctx) as response:
            status = response.getcode()
            body = json.loads(response.read().decode("utf-8"))
            content = body["choices"][0]["message"]["content"]
            print(f"  ✓ HTTP Status: {status}")
            print(f"  ✓ Model: {body.get('model', 'N/A')}")
            print(f"  ✓ Content: {content!r}")

        print("\n" + "=" * 60)
        print(f"✓ TEST PASSED: {label} inference reachable")
        print("=" * 60)
        return True

    except urllib.error.HTTPError as e:
        if e.code in (401, 403):
            print(f"  ⚠ Gateway rejected the caller token (HTTP {e.code}).")
            print("  This is expected from a developer identity - the gateway")
            print("  validates the PROJECT managed identity. Use the agent test")
            print("  (below) for the authoritative end-to-end validation.")
            print(f"\n⚠ TEST SKIPPED: {label} (token not accepted by gateway)")
            return None
        detail = e.read().decode("utf-8", errors="ignore")[:300]
        print(f"\n✗ TEST FAILED: HTTP {e.code} - {detail}")
        return False

    except Exception as e:
        print(f"\n✗ TEST FAILED: {str(e)}")
        import traceback

        traceback.print_exc()
        return False


def _run_agent(model: str, agent_name: str, label: str, cleanup_agent: bool = True):
    """Create an agent with the given model, run one prompt, and clean up."""
    agent = None
    try:
        with (
            DefaultAzureCredential() as credential,
            AIProjectClient(credential=credential, endpoint=PROJECT_ENDPOINT) as project_client,
            project_client.get_openai_client() as openai_client,
        ):
            print(f"✓ Connected to AI Project at {PROJECT_ENDPOINT}")

            agent = project_client.agents.create_version(
                agent_name=agent_name,
                definition=PromptAgentDefinition(
                    model=model,
                    instructions="You are a helpful assistant. Answer briefly and concisely.",
                ),
            )
            print(f"✓ Created agent (id: {agent.id}, name: {agent.name}, version: {agent.version})")
            print(f"  Model: {model}")

            response = openai_client.responses.create(
                input="Say hello and confirm you are working. Keep it to one short sentence.",
                extra_body={"agent_reference": {"name": agent.name, "type": "agent_reference"}},
            )
            log_response_info(response, f"{label} Response")

            print(f"\n✓ Agent response: {response.output_text}")

            if cleanup_agent:
                project_client.agents.delete_version(
                    agent_name=agent.name, agent_version=agent.version
                )
                print(f"  Cleaned up agent: {agent.name}")
            else:
                print(f"  Preserved agent version: {agent.name}:{agent.version}")

            if response.output_text:
                print(f"\n✓ TEST PASSED: {label}")
                return True
            print(f"\n✗ TEST FAILED: {label} returned no text")
            return False

    except Exception as e:
        error_str = str(e)
        print(f"\n✗ TEST FAILED: {error_str}")
        log_exception_info(e, f"{label} Error")

        if "424" in error_str or "Failed Dependency" in error_str:
            print("\n  ⚠ Known Issue: cross-region backend not reachable.")
            print("  The APIM gateway or the cross-region private endpoint could")
            print("  not reach the backend Foundry account. Verify the backend")
            print("  private endpoint DNS and the APIM managed-identity role.")
        elif "DeploymentNotFound" in error_str or "404" in error_str:
            print("\n  ⚠ Check BYOM_CONNECTION_NAME / BYOM_DEPLOYMENT_NAME match")
            print("  the deployed connection (default 'ai-gateway/gpt-5').")

        import traceback

        traceback.print_exc()

        if agent is not None and cleanup_agent:
            try:
                with (
                    DefaultAzureCredential() as credential,
                    AIProjectClient(credential=credential, endpoint=PROJECT_ENDPOINT) as project_client,
                ):
                    project_client.agents.delete_version(
                        agent_name=agent.name, agent_version=agent.version
                    )
                    print(f"  Cleaned up agent: {agent.name}")
            except Exception:
                pass
        return False


def test_agent_via_byom_model(model: str, cleanup_agent: bool = True):
    """Create an agent that uses the BYOM (APIM AI Gateway) model reference."""
    print("\n" + "=" * 60)
    print("TEST: Agent via BYO Model (APIM AI Gateway)")
    print("=" * 60)
    return _run_agent(
        model=model,
        agent_name="byom-apim-gateway-test",
        label="Agent via BYOM model",
        cleanup_agent=cleanup_agent,
    )


def test_basic_agent(model: str, cleanup_agent: bool = True):
    """Sanity check: a basic agent on the project-region (local) model."""
    print("\n" + "=" * 60)
    print("TEST: Basic Agent (project-region fallback model)")
    print("=" * 60)
    return _run_agent(
        model=model,
        agent_name="basic-fallback-test",
        label="Basic agent (fallback model)",
        cleanup_agent=cleanup_agent,
    )


def main():
    parser = argparse.ArgumentParser(
        description="BYO Model via APIM AI Gateway - Agents v2 Test Script",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  python test_byo_model_apim_gateway_agent_v2.py                 # Run all tests
  python test_byo_model_apim_gateway_agent_v2.py --test byom     # Only the BYOM agent test
  python test_byo_model_apim_gateway_agent_v2.py --test gateway  # Only the direct APIM test
  python test_byo_model_apim_gateway_agent_v2.py --test basic    # Only the fallback model test

Environment variables:
  PROJECT_ENDPOINT        - Azure AI project (Foundry API) endpoint
  BYOM_CONNECTION_NAME    - AI Gateway connection name (default: ai-gateway)
  BYOM_DEPLOYMENT_NAME    - Backend deployment name (default: gpt-5)
  BYOM_MODEL              - Full model reference (default: <connection>/<deployment>)
  FALLBACK_MODEL_NAME     - Project-region local model (default: gpt-4o)
  APIM_GATEWAY_URL        - APIM gateway base URL for the direct test (optional)
  INFERENCE_API_PATH      - APIM inference API path (default: inference)
  INFERENCE_API_VERSION   - Inference API version (default: 2024-10-21)
""",
    )
    parser.add_argument(
        "--test",
        choices=["gateway", "byom", "basic", "all"],
        default="all",
        help="Which test(s) to run (default: all)",
    )
    parser.add_argument(
        "--keep-agent",
        action="store_true",
        help="Preserve created agent versions instead of deleting them",
    )
    args = parser.parse_args()

    cleanup = not args.keep_agent

    print("=" * 60)
    print("BYO MODEL via APIM AI GATEWAY - AGENTS v2 TEST")
    print("=" * 60)
    print("\nConfiguration:")
    print(f"  Project Endpoint:      {PROJECT_ENDPOINT}")
    print(f"  BYOM Model:            {BYOM_MODEL}")
    print(f"  Fallback Model:        {FALLBACK_MODEL_NAME}")
    print(f"  APIM Gateway URL:      {APIM_GATEWAY_URL or '(not set - direct test skipped)'}")
    print(f"  Inference API Version: {INFERENCE_API_VERSION}")

    results = {}

    if args.test in ["gateway", "all"]:
        result = test_apim_gateway_inference(APIM_GATEWAY_URL, BYOM_DEPLOYMENT_NAME)
        if result is not None:
            results["gateway_direct"] = result

    if args.test in ["byom", "all"]:
        results["agent_byom"] = test_agent_via_byom_model(BYOM_MODEL, cleanup_agent=cleanup)

    if args.test in ["basic", "all"]:
        results["agent_basic"] = test_basic_agent(FALLBACK_MODEL_NAME, cleanup_agent=cleanup)

    # Summary
    print("\n" + "=" * 60)
    print("TEST SUMMARY")
    print("=" * 60)

    if not results:
        print("  No tests were run.")
        return 1

    for test_name, passed in results.items():
        status = "✓ PASSED" if passed else "✗ FAILED"
        print(f"  {test_name}: {status}")

    all_passed = all(results.values())
    print("\n" + "=" * 60)
    if all_passed:
        print("ALL TESTS PASSED!")
    else:
        print("SOME TESTS FAILED")
        print("Note: if the Foundry account is private, run this from inside the")
        print("      VNet (VPN / ExpressRoute / Bastion). See TESTING-GUIDE.md.")
    print("=" * 60)

    return 0 if all_passed else 1


if __name__ == "__main__":
    sys.exit(main())
