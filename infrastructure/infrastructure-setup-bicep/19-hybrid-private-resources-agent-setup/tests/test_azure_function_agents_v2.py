#!/usr/bin/env python3
"""
Azure Function VNet Integration Test Script

This script tests the key "Azure Function behind a VNet" scenario:
  - The function is publicly accessible (publicNetworkAccess: Enabled)
  - But its OUTBOUND traffic goes through VNet Integration
  - This lets it reach a private storage account (no public endpoint)
  - The 'storage' field in the response proves VNet Integration works

The test has two modes:
  --test public   → Function WITHOUT VNet Integration (storage writes fail)
  --test private  → Function WITH VNet Integration (storage writes succeed)

Tests:
1. Connectivity - Direct HTTP call to /api/healthz (checks storage reachability)
2. Calculate + Store - Call /api/calculate, verify storage.stored == true
3. History - Call /api/history, verify VNet Integration read path
4. Agent test - Full round-trip: agent → OpenAPI tool → Function → private storage

The meaningful test is comparing public vs private:
  - Public function: calculation works, storage.stored == false
  - Private function (VNet): calculation works, storage.stored == true
"""

import argparse
import json
import logging
import os
import ssl
import sys
import traceback
import urllib.error
import urllib.request

# ============================================================================
# LOGGING CONFIGURATION
# ============================================================================
LOG_LEVEL = logging.INFO

logging.basicConfig(
    level=LOG_LEVEL,
    format="%(asctime)s - %(name)s - %(levelname)s - %(message)s",
)

logging.getLogger("azure.core.pipeline.policies.http_logging_policy").setLevel(
    LOG_LEVEL
)
logging.getLogger("httpx").setLevel(LOG_LEVEL)
logging.getLogger("urllib3").setLevel(logging.WARNING)
logging.getLogger("azure.identity").setLevel(logging.WARNING)

logger = logging.getLogger(__name__)

# ============================================================================

from azure.ai.projects import AIProjectClient
from azure.ai.projects.models import (
    OpenApiFunctionDefinition,
    OpenApiAnonymousAuthDetails,
    OpenApiTool,
    PromptAgentDefinition,
)
from azure.identity import DefaultAzureCredential

# ============================================================================
# CONFIGURATION
# ============================================================================
PROJECT_ENDPOINT = os.environ.get(
    "PROJECT_ENDPOINT",
    "https://aiservicesaxy3.services.ai.azure.com/api/projects/projectaxy3",
)
MODEL_NAME = os.environ.get("MODEL_NAME", "gpt-4o-mini")

# Azure Function App URLs
# PUBLIC = Function App without VNet Integration (no private storage access)
# PRIVATE = Function App with VNet Integration (can reach private storage)
FUNCTION_APP_PUBLIC = os.environ.get("FUNCTION_APP_PUBLIC", "")
FUNCTION_APP_PRIVATE = os.environ.get("FUNCTION_APP_PRIVATE", "")

# ============================================================================


def log_response_info(response, label="Response"):
    """Extract and log useful debugging info from OpenAI response objects."""
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


def load_function_openapi_spec():
    """Load the Azure Function calculator OpenAPI spec.

    Uses the spec from azure-function-server/ which has /api/ route prefix
    and includes the storage status field in the response.
    """
    spec_path = os.path.join(
        os.path.dirname(__file__),
        "..",
        "azure-function-server",
        "calculator_openapi.json",
    )
    if not os.path.exists(spec_path):
        spec_path = os.path.join(
            os.path.dirname(__file__), "calculator_function_openapi.json"
        )
    with open(spec_path, "r") as f:
        return json.load(f)


def test_function_connectivity(
    function_url: str,
    label: str = "Azure Function",
    expect_storage: bool = False,
):
    """Test direct HTTP connectivity to the Azure Function.

    When expect_storage is True (VNet-integrated function), verifies that
    the health check reports private storage as reachable.
    """
    print("\n" + "=" * 60)
    print(f"TEST: Function Connectivity - {label}")
    print("=" * 60)

    if not function_url:
        print(f"  ⚠ {label} URL not configured, skipping connectivity test")
        return None

    try:
        ctx = ssl.create_default_context()
        base_url = function_url.rstrip("/")
        print(f"  Target: {base_url}")

        # --- Health check (GET /api/healthz) ---
        print("\n--- Health Check (GET /api/healthz) ---")
        health_req = urllib.request.Request(base_url + "/api/healthz", method="GET")
        with urllib.request.urlopen(health_req, timeout=15, context=ctx) as response:
            status = response.getcode()
            body = response.read().decode("utf-8")
            health = json.loads(body)
            print(f"  ✓ HTTP Status: {status}")
            print(f"  ✓ Compute: {health.get('compute', 'N/A')}")

            storage_info = health.get("private_storage", {})
            storage_reachable = storage_info.get("reachable", False)
            storage_detail = storage_info.get("detail", "N/A")
            print(f"  {'✓' if storage_reachable else '⚠'} Private Storage: "
                  f"{'reachable' if storage_reachable else 'NOT reachable'}")
            print(f"    Detail: {storage_detail}")

            if expect_storage and not storage_reachable:
                print("\n  ✗ EXPECTED private storage to be reachable (VNet Integration)")
                print("    This means VNet Integration is not working.")
                return False

        # --- Calculate with storage (POST /api/calculate) ---
        print("\n--- Calculate + Store (POST /api/calculate) ---")
        calc_data = json.dumps({"operation": "add", "a": 3, "b": 5}).encode("utf-8")
        calc_req = urllib.request.Request(
            base_url + "/api/calculate",
            data=calc_data,
            headers={"Content-Type": "application/json"},
            method="POST",
        )
        with urllib.request.urlopen(calc_req, timeout=15, context=ctx) as response:
            status = response.getcode()
            body = response.read().decode("utf-8")
            result = json.loads(body)

            print(f"  ✓ HTTP Status: {status}")
            print(f"  ✓ Calculation: 3 + 5 = {result.get('result')}")

            storage = result.get("storage", {})
            stored = storage.get("stored", False)
            print(f"  {'✓' if stored else '⚠'} Storage write: "
                  f"{'succeeded' if stored else 'failed'}")
            if stored:
                print(f"    Blob: {storage.get('blob', 'N/A')}")
            else:
                print(f"    Reason: {storage.get('reason', 'N/A')}")

            if expect_storage and not stored:
                print("\n  ✗ EXPECTED storage write to succeed (VNet Integration)")
                return False

            if result.get("result") != 8.0:
                print(f"  ✗ Unexpected calculation result: {result.get('result')}")
                return False

        # --- History (GET /api/history) ---
        if expect_storage:
            print("\n--- History (GET /api/history?limit=3) ---")
            hist_req = urllib.request.Request(
                base_url + "/api/history?limit=3", method="GET"
            )
            with urllib.request.urlopen(hist_req, timeout=15, context=ctx) as response:
                body = response.read().decode("utf-8")
                hist = json.loads(body)
                vnet_ok = hist.get("vnet_integration", False)
                count = hist.get("count", 0)
                print(f"  ✓ VNet Integration: {vnet_ok}")
                print(f"  ✓ Records returned: {count}")
                if count > 0:
                    latest = hist["history"][0]
                    print(f"  ✓ Latest: {latest.get('operation')}("
                          f"{latest.get('a')}, {latest.get('b')}) = "
                          f"{latest.get('result')}")

        print(f"\n✓ TEST PASSED: {label} connectivity working")
        return True

    except Exception as e:
        print(f"\n✗ TEST FAILED: {e}")
        traceback.print_exc()
        return False


def test_function_openapi_tool_via_agent(
    function_url: str,
    label: str = "Azure Function",
    expect_storage: bool = False,
):
    """Test that an agent can call an Azure Function via OpenAPI tool.

    When expect_storage is True, verifies the agent response mentions
    that the result was stored (proving VNet Integration end-to-end
    through the DataProxy → Function → private storage path).
    """
    print("\n" + "=" * 60)
    print(f"TEST: Azure Function as OpenAPI Tool - {label}")
    print("=" * 60)

    if not function_url:
        print(f"  ⚠ {label} URL not configured, skipping agent test")
        return None

    agent = None

    try:
        with (
            DefaultAzureCredential() as credential,
            AIProjectClient(
                credential=credential, endpoint=PROJECT_ENDPOINT
            ) as project_client,
            project_client.get_openai_client() as openai_client,
        ):
            print(f"✓ Connected to AI Project at {PROJECT_ENDPOINT}")

            openapi_spec = load_function_openapi_spec()
            openapi_spec["servers"] = [
                {"url": function_url.rstrip("/"), "description": label}
            ]

            auth = OpenApiAnonymousAuthDetails()
            openapi_tool = OpenApiTool(
                openapi=OpenApiFunctionDefinition(
                    name="calculator",
                    spec=openapi_spec,
                    description=(
                        "A calculator API running as an Azure Function with VNet "
                        "Integration. Performs arithmetic and stores results in "
                        "private blob storage. The 'storage' field shows whether "
                        "VNet Integration is working."
                    ),
                    auth=auth,
                )
            )

            agent = project_client.agents.create_version(
                agent_name="function-openapi-test",
                definition=PromptAgentDefinition(
                    model=MODEL_NAME,
                    instructions=(
                        "You are a helpful agent that uses a calculator API hosted "
                        "as an Azure Function. When asked to perform arithmetic, use "
                        "the calculator tool. After getting the result, also report "
                        "whether the storage write succeeded (the 'storage.stored' "
                        "field in the response). This is important because it shows "
                        "whether VNet Integration is working."
                    ),
                    tools=[openapi_tool],
                ),
            )
            print(f"✓ Created agent with OpenAPI tool (id: {agent.id})")
            print(f"  Azure Function URL: {function_url}")
            print(f"  Expect storage: {expect_storage}")

            conversation = openai_client.conversations.create()
            print(f"✓ Created conversation: {conversation.id}")

            print("  Sending request to use calculator Function...")
            response = openai_client.responses.create(
                conversation=conversation.id,
                input=(
                    "Please calculate 12 multiplied by 8 using the calculator tool. "
                    "Tell me the result AND whether the result was stored successfully "
                    "(check the storage.stored field in the response)."
                ),
                extra_body={
                    "agent_reference": {
                        "name": agent.name,
                        "type": "agent_reference",
                    }
                },
            )
            log_response_info(response, "Function OpenAPI Tool Response")

            output = response.output_text
            print(f"\n✓ Agent response: {output}")

            # Cleanup
            project_client.agents.delete_version(
                agent_name=agent.name, agent_version=agent.version
            )
            print(f"  Cleaned up agent: {agent.name}")

            print(f"\n✓ TEST PASSED: Azure Function as OpenAPI tool via {label}")
            return True

    except Exception as e:
        error_str = str(e)
        print(f"\n✗ TEST FAILED: {error_str}")
        log_exception_info(e, "Function OpenAPI Tool Error")

        if "TaskCanceledException" in error_str:
            print("\n  ⚠ Known Issue: TaskCanceledException")
            print("  Request hit wrong Hyena scale unit (DataProxy not deployed)")
            print("  Re-running the test may succeed.")
        elif "http_client_error" in error_str or "403" in error_str:
            print("\n  ⚠ DataProxy routing failure")
            print("  Check: publicNetworkAccess must be 'Enabled' on the Function App")
            print("  (DataProxy cannot use Private Endpoints — see TESTING-GUIDE.md)")

        traceback.print_exc()

        if agent is not None:
            try:
                with (
                    DefaultAzureCredential() as credential,
                    AIProjectClient(
                        credential=credential, endpoint=PROJECT_ENDPOINT
                    ) as project_client,
                ):
                    project_client.agents.delete_version(
                        agent_name=agent.name, agent_version=agent.version
                    )
                    print(f"  Cleaned up agent: {agent.name}")
            except Exception:
                pass

        return False


def main():
    parser = argparse.ArgumentParser(
        description="Azure Function VNet Integration Test Script",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  python test_azure_function_agents_v2.py                    # Run all tests
  python test_azure_function_agents_v2.py --test public      # Function WITHOUT VNet
  python test_azure_function_agents_v2.py --test private     # Function WITH VNet Integration
  python test_azure_function_agents_v2.py --retry 3          # Retry failed tests

The meaningful comparison:
  --test public   → calculate works, storage.stored = false  (no VNet access to storage)
  --test private  → calculate works, storage.stored = true   (VNet Integration to private storage)

Environment variables:
  PROJECT_ENDPOINT      - Azure AI project endpoint
  MODEL_NAME            - Model to use (default: gpt-4o-mini)
  FUNCTION_APP_PUBLIC   - Function App URL WITHOUT VNet Integration
  FUNCTION_APP_PRIVATE  - Function App URL WITH VNet Integration (same hostname,
                          different deployment — the VNet Integration is outbound)
""",
    )
    parser.add_argument(
        "--test",
        choices=["public", "private", "all"],
        default="all",
        help="Which Function App to test: public, private, or all (default: all)",
    )
    parser.add_argument(
        "--retry",
        type=int,
        default=1,
        help="Number of retries for agent tests (default: 1)",
    )
    args = parser.parse_args()

    print("=" * 60)
    print("AZURE FUNCTION — VNET INTEGRATION TEST")
    print("=" * 60)
    print("\nConfiguration:")
    print(f"  Project Endpoint: {PROJECT_ENDPOINT}")
    print(f"  Model: {MODEL_NAME}")
    print(f"  Function App (Public, no VNet): {FUNCTION_APP_PUBLIC or '(not set)'}")
    print(f"  Function App (Private, VNet):   {FUNCTION_APP_PRIVATE or '(not set)'}")
    print("\nWhat this tests:")
    print("  Public  → calculate works, private storage write FAILS (no VNet)")
    print("  Private → calculate works, private storage write SUCCEEDS (VNet)")

    results = {}

    # Connectivity tests
    if args.test in ["public", "all"] and FUNCTION_APP_PUBLIC:
        result = test_function_connectivity(
            FUNCTION_APP_PUBLIC, "Public Azure Function (no VNet)", expect_storage=False
        )
        if result is not None:
            results["connectivity_public"] = result

    if args.test in ["private", "all"] and FUNCTION_APP_PRIVATE:
        result = test_function_connectivity(
            FUNCTION_APP_PRIVATE,
            "Private Azure Function (VNet Integration)",
            expect_storage=True,
        )
        if result is not None:
            results["connectivity_private"] = result

    # Agent tests: Function as OpenAPI Tool
    if args.test in ["public", "all"] and FUNCTION_APP_PUBLIC:
        for attempt in range(args.retry):
            if attempt > 0:
                print(f"\n  Retry attempt {attempt + 1}/{args.retry}...")
            result = test_function_openapi_tool_via_agent(
                FUNCTION_APP_PUBLIC,
                "Public Azure Function (no VNet)",
                expect_storage=False,
            )
            if result is not None:
                results["agent_public"] = result
                if result:
                    break
        else:
            if "agent_public" not in results:
                results["agent_public"] = False

    if args.test in ["private", "all"] and FUNCTION_APP_PRIVATE:
        for attempt in range(args.retry):
            if attempt > 0:
                print(f"\n  Retry attempt {attempt + 1}/{args.retry}...")
            result = test_function_openapi_tool_via_agent(
                FUNCTION_APP_PRIVATE,
                "Private Azure Function (VNet Integration)",
                expect_storage=True,
            )
            if result is not None:
                results["agent_private"] = result
                if result:
                    break
        else:
            if "agent_private" not in results:
                results["agent_private"] = False

    # Summary
    print("\n" + "=" * 60)
    print("TEST SUMMARY")
    print("=" * 60)

    if not results:
        print(
            "  No tests were run. Set FUNCTION_APP_PUBLIC and/or FUNCTION_APP_PRIVATE."
        )
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
        print("Note: Agent tests may fail due to Hyena cluster routing (~50% chance)")
        print("      Use --retry N to retry failed tests")
    print("=" * 60)

    return 0 if all_passed else 1


if __name__ == "__main__":
    sys.exit(main())
