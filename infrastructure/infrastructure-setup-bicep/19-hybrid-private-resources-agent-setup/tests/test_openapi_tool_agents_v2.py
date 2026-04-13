#!/usr/bin/env python3
"""
OpenAPI Tool Test Script

This script tests OpenAPI tool integration with Azure AI Foundry Agents v2,
validating that agents can call OpenAPI-spec HTTP services through the Data Proxy
when those services run behind a private VNet.

Tests:
1. OpenAPI Connectivity (Direct HTTP) - Direct REST call to the OpenAPI service
2. OpenAPI Tool via Agent (Public) - Test OpenAPI tool via public Container App
3. OpenAPI Tool via Agent (Private) - Test OpenAPI tool via private Container App (VNet)

Note: Agent tests may intermittently fail due to known Hyena cluster routing
issue where ~50% of requests hit a scale unit without Data Proxy deployed.
"""

import os
import sys
import logging
import argparse
import json
import urllib.request
import urllib.error
import ssl

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
from azure.ai.projects.models import (
    OpenApiTool,
    OpenApiFunctionDefinition,
    OpenApiAnonymousAuthDetails,
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

# OpenAPI server URLs
# Public (external, accessible from anywhere)
OPENAPI_SERVER_PUBLIC = os.environ.get(
    "OPENAPI_SERVER_PUBLIC",
    "",  # Set to your public Container App URL, e.g. https://openapi-server-public.<env>.<region>.azurecontainerapps.io
)

# Private (internal, only accessible from VNet via Data Proxy)
OPENAPI_SERVER_PRIVATE = os.environ.get(
    "OPENAPI_SERVER_PRIVATE",
    "",  # Set to your private Container App URL, e.g. https://openapi-server.<env>.<region>.azurecontainerapps.io
)

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


def load_openapi_spec():
    """Load the calculator OpenAPI spec from the JSON file next to this script."""
    spec_path = os.path.join(os.path.dirname(__file__), "calculator_openapi.json")
    with open(spec_path, "r") as f:
        return json.load(f)


def test_openapi_connectivity(server_url: str, label: str = "OpenAPI Server"):
    """Test direct HTTP connectivity to the OpenAPI service."""
    print("\n" + "=" * 60)
    print(f"TEST: OpenAPI Connectivity - {label}")
    print("=" * 60)

    if not server_url:
        print(f"  ⚠ {label} URL not configured, skipping connectivity test")
        return None

    try:
        ctx = ssl.create_default_context()

        # Test health endpoint
        print(f"  Target: {server_url}")
        print("\n--- Health Check ---")

        health_url = server_url.rstrip("/") + "/healthz"
        health_req = urllib.request.Request(health_url, method="GET")

        with urllib.request.urlopen(health_req, timeout=15, context=ctx) as response:
            status = response.getcode()
            body = response.read().decode("utf-8")
            print(f"  ✓ HTTP Status: {status}")
            print(f"  ✓ Response: {body}")

        # Test calculate endpoint
        print("\n--- Calculate (POST /calculate) ---")

        calc_data = json.dumps({"operation": "add", "a": 2, "b": 4}).encode("utf-8")

        calc_req = urllib.request.Request(
            server_url.rstrip("/") + "/calculate",
            data=calc_data,
            headers={"Content-Type": "application/json"},
            method="POST",
        )

        with urllib.request.urlopen(calc_req, timeout=15, context=ctx) as response:
            status = response.getcode()
            body = response.read().decode("utf-8")
            result = json.loads(body)

            print(f"  ✓ HTTP Status: {status}")
            print(f"  ✓ Response: {body}")

            if result.get("result") == 6.0:
                print("  ✓ Calculation correct: 2 + 4 = 6")
            else:
                print(f"  ⚠ Unexpected result: {result.get('result')}")

        # Test OpenAPI spec endpoint
        print("\n--- OpenAPI Spec (GET /openapi.json) ---")

        spec_req = urllib.request.Request(
            server_url.rstrip("/") + "/openapi.json", method="GET"
        )

        with urllib.request.urlopen(spec_req, timeout=15, context=ctx) as response:
            status = response.getcode()
            body = response.read().decode("utf-8")
            spec = json.loads(body)

            print(f"  ✓ HTTP Status: {status}")
            print(f"  ✓ API Title: {spec.get('info', {}).get('title', 'N/A')}")
            print(
                f"  ✓ Paths: {list(spec.get('paths', {}).keys())}"
            )

        print("\n" + "=" * 60)
        print(f"✓ TEST PASSED: {label} connectivity working")
        print("=" * 60)
        return True

    except Exception as e:
        print(f"\n✗ TEST FAILED: {str(e)}")
        import traceback

        traceback.print_exc()
        return False


def test_openapi_tool_via_agent(server_url: str, label: str = "OpenAPI Server"):
    """Test that an agent can use an OpenAPI tool via the Data Proxy."""
    print("\n" + "=" * 60)
    print(f"TEST: OpenAPI Tool via Agent - {label}")
    print("=" * 60)

    if not server_url:
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

            # Load OpenAPI spec
            openapi_spec = load_openapi_spec()

            # Override the server URL in the spec to point to our deployed service
            openapi_spec["servers"] = [
                {"url": server_url.rstrip("/"), "description": label}
            ]

            # Create OpenAPI tool with anonymous auth (for testing)
            auth = OpenApiAnonymousAuthDetails()
            openapi_tool = OpenApiTool(
                openapi=OpenApiFunctionDefinition(
                    name="calculator",
                    spec=openapi_spec,
                    description="A calculator API that can perform arithmetic operations (add, subtract, multiply, divide)",
                    auth=auth,
                )
            )

            # Create agent with OpenAPI tool
            agent = project_client.agents.create_version(
                agent_name="openapi-tool-test",
                definition=PromptAgentDefinition(
                    model=MODEL_NAME,
                    instructions="""You are a helpful agent that can use a calculator API.
                    When asked to perform arithmetic, use the calculator tool's /calculate endpoint.
                    Report the exact result from the API response.""",
                    tools=[openapi_tool],
                ),
            )
            print(f"✓ Created agent with OpenAPI tool (id: {agent.id})")
            print(f"  OpenAPI Server URL: {server_url}")

            # Create conversation
            conversation = openai_client.conversations.create()
            print(f"✓ Created conversation: {conversation.id}")

            # Send request that triggers the OpenAPI tool
            print("  Sending request to use calculator API...")
            response = openai_client.responses.create(
                conversation=conversation.id,
                input="Please calculate 15 multiplied by 7 using the calculator tool and tell me the result.",
                extra_body={
                    "agent_reference": {"name": agent.name, "type": "agent_reference"}
                },
            )
            log_response_info(response, "OpenAPI Tool Response")

            print(f"\n✓ Agent response: {response.output_text}")

            # Cleanup
            project_client.agents.delete_version(
                agent_name=agent.name, agent_version=agent.version
            )
            print(f"  Cleaned up agent: {agent.name}")

            print(f"\n✓ TEST PASSED: OpenAPI tool via {label}")
            return True

    except Exception as e:
        error_str = str(e)
        print(f"\n✗ TEST FAILED: {error_str}")
        log_exception_info(e, "OpenAPI Tool Error")

        if "TaskCanceledException" in error_str:
            print("\n  ⚠ Known Issue: TaskCanceledException")
            print("  This occurs when request hits the wrong Hyena scale unit")
            print("  (Data Proxy is only deployed on one of two scale units)")
            print("  Re-running the test may succeed on the next attempt.")
        elif "424" in error_str or "Failed Dependency" in error_str:
            print("\n  ⚠ Known Issue: DNS Resolution")
            print("  Data Proxy cannot resolve private Container Apps DNS.")

        import traceback

        traceback.print_exc()

        # Cleanup agent if created
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
        description="OpenAPI Tool Test Script",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  python test_openapi_tool_agents_v2.py                    # Run all tests
  python test_openapi_tool_agents_v2.py --test public      # Only public server tests
  python test_openapi_tool_agents_v2.py --test private     # Only private server tests
  python test_openapi_tool_agents_v2.py --retry 3          # Retry failed agent tests

Environment variables:
  PROJECT_ENDPOINT         - Azure AI project endpoint
  MODEL_NAME               - Model to use (default: gpt-4o-mini)
  OPENAPI_SERVER_PUBLIC    - Public OpenAPI server URL
  OPENAPI_SERVER_PRIVATE   - Private OpenAPI server URL (VNet-internal)
""",
    )
    parser.add_argument(
        "--test",
        choices=["public", "private", "all"],
        default="all",
        help="Which OpenAPI server to test: public, private, or all (default: all)",
    )
    parser.add_argument(
        "--retry",
        type=int,
        default=1,
        help="Number of retries for agent tests (default: 1)",
    )
    args = parser.parse_args()

    print("=" * 60)
    print("OPENAPI TOOL TEST")
    print("=" * 60)
    print(f"\nConfiguration:")
    print(f"  Project Endpoint: {PROJECT_ENDPOINT}")
    print(f"  Model: {MODEL_NAME}")
    print(f"  Public OpenAPI Server: {OPENAPI_SERVER_PUBLIC or '(not set)'}")
    print(f"  Private OpenAPI Server: {OPENAPI_SERVER_PRIVATE or '(not set)'}")

    results = {}

    # Connectivity test
    if args.test in ["public", "all"] and OPENAPI_SERVER_PUBLIC:
        result = test_openapi_connectivity(OPENAPI_SERVER_PUBLIC, "Public OpenAPI Server")
        if result is not None:
            results["connectivity_public"] = result

    if args.test in ["private", "all"] and OPENAPI_SERVER_PRIVATE:
        result = test_openapi_connectivity(
            OPENAPI_SERVER_PRIVATE, "Private OpenAPI Server"
        )
        if result is not None:
            results["connectivity_private"] = result

    # Agent tests: OpenAPI Tool via Agent (Public)
    if args.test in ["public", "all"] and OPENAPI_SERVER_PUBLIC:
        for attempt in range(args.retry):
            if attempt > 0:
                print(f"\n  Retry attempt {attempt + 1}/{args.retry}...")
            result = test_openapi_tool_via_agent(
                OPENAPI_SERVER_PUBLIC, "Public OpenAPI Server"
            )
            if result is not None:
                results["agent_public"] = result
                if result:
                    break
        else:
            if "agent_public" not in results:
                results["agent_public"] = False

    # Agent tests: OpenAPI Tool via Agent (Private)
    if args.test in ["private", "all"] and OPENAPI_SERVER_PRIVATE:
        for attempt in range(args.retry):
            if attempt > 0:
                print(f"\n  Retry attempt {attempt + 1}/{args.retry}...")
            result = test_openapi_tool_via_agent(
                OPENAPI_SERVER_PRIVATE, "Private OpenAPI Server"
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
        print("  No tests were run. Set OPENAPI_SERVER_PUBLIC and/or OPENAPI_SERVER_PRIVATE.")
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
