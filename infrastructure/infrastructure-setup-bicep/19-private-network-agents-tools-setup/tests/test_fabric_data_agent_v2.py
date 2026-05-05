#!/usr/bin/env python3
"""
Fabric Data Agent Test Script

This script tests Microsoft Fabric Data Agent integration with Azure AI Foundry
Agents v2, validating that agents can leverage Fabric data agents through the
Data Proxy, including when Fabric workspaces use private endpoints (VNet).

Tests:
1. Fabric Connectivity - Verify the Fabric workspace connection is reachable
2. Fabric Data Agent via Agent (Public) - Test via public Fabric workspace
3. Fabric Data Agent via Agent (Private) - Test via private Fabric workspace (VNet)

Prerequisites (from Fabric team):
- Deploy a Fabric Capacity (F2 SKU)
- Create a workspace at https://app.fabric.microsoft.com/home using that capacity
- For private: Enable workspace-level private endpoints
- Add your Foundry Project's Identity as a contributor to the workspace
- Create a Fabric Data Agent and connect it to your Foundry Agent
- Create a project connection for the Fabric workspace

Note: Agent tests may intermittently fail due to known Hyena cluster routing
issue where ~50% of requests hit a scale unit without Data Proxy deployed.
"""

import os
import sys
import logging
import argparse

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

# ============================================================================

from azure.ai.projects import AIProjectClient
from azure.ai.projects.models import (
    MicrosoftFabricPreviewTool,
    FabricDataAgentToolParameters,
    ToolProjectConnection,
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

# Fabric Data Agent connection IDs (project connection references)
# Public (Fabric workspace without private endpoints)
FABRIC_CONNECTION_ID_PUBLIC = os.environ.get("FABRIC_CONNECTION_ID_PUBLIC", "")

# Private (Fabric workspace with private endpoints, only accessible via VNet)
FABRIC_CONNECTION_ID_PRIVATE = os.environ.get("FABRIC_CONNECTION_ID_PRIVATE", "")

# Optional: Test query to send to the Fabric data agent
FABRIC_TEST_QUERY = os.environ.get(
    "FABRIC_TEST_QUERY",
    "What data is available? Please summarize the tables or datasets you can access.",
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


def test_fabric_connectivity(connection_id: str, label: str = "Fabric"):
    """Test that the Fabric project connection exists and is accessible."""
    print("\n" + "=" * 60)
    print(f"TEST: Fabric Connectivity - {label}")
    print("=" * 60)

    if not connection_id:
        print(f"  ⚠ {label} connection ID not configured, skipping connectivity test")
        return None

    try:
        with (
            DefaultAzureCredential() as credential,
            AIProjectClient(
                credential=credential, endpoint=PROJECT_ENDPOINT
            ) as project_client,
        ):
            print(f"  ✓ Connected to AI Project at {PROJECT_ENDPOINT}")
            print(f"  Connection ID: {connection_id}")

            # Verify the connection exists by listing connections and checking
            connection = project_client.connections.get(connection_id)
            print(f"  ✓ Connection found: {connection.name}")
            print(f"  ✓ Connection type: {connection.type}")

            if hasattr(connection, "target") and connection.target:
                print(f"  ✓ Target: {connection.target}")

            print("\n" + "=" * 60)
            print(f"✓ TEST PASSED: {label} connectivity verified")
            print("=" * 60)
            return True

    except Exception as e:
        print(f"\n✗ TEST FAILED: {str(e)}")
        log_exception_info(e, "Fabric Connectivity Error")
        import traceback

        traceback.print_exc()
        return False


def test_fabric_tool_via_agent(connection_id: str, label: str = "Fabric"):
    """Test that an agent can use a Fabric Data Agent tool via the Data Proxy."""
    print("\n" + "=" * 60)
    print(f"TEST: Fabric Data Agent via Agent - {label}")
    print("=" * 60)

    if not connection_id:
        print(f"  ⚠ {label} connection ID not configured, skipping agent test")
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

            # Create Fabric Data Agent tool
            fabric_tool = MicrosoftFabricPreviewTool(
                fabric_dataagent_preview=FabricDataAgentToolParameters(
                    project_connections=[
                        ToolProjectConnection(
                            project_connection_id=connection_id,
                        )
                    ]
                )
            )

            # Create agent with Fabric tool
            agent = project_client.agents.create_version(
                agent_name="fabric-data-agent-test",
                definition=PromptAgentDefinition(
                    model=MODEL_NAME,
                    instructions="""You are a helpful agent that can query data using Microsoft Fabric.
                    When asked about data, use the Fabric data agent tool to look up information.
                    Report what you find from the Fabric data source.""",
                    tools=[fabric_tool],
                ),
            )
            print(f"✓ Created agent with Fabric tool (id: {agent.id})")
            print(f"  Fabric Connection ID: {connection_id}")

            # Create conversation
            conversation = openai_client.conversations.create()
            print(f"✓ Created conversation: {conversation.id}")

            # Send request that triggers the Fabric tool
            print(f"  Sending query: {FABRIC_TEST_QUERY}")
            response = openai_client.responses.create(
                conversation=conversation.id,
                input=FABRIC_TEST_QUERY,
                extra_body={
                    "agent_reference": {
                        "name": agent.name,
                        "type": "agent_reference",
                    }
                },
            )
            log_response_info(response, "Fabric Tool Response")

            print(f"\n✓ Agent response: {response.output_text}")

            # Cleanup
            project_client.agents.delete_version(
                agent_name=agent.name, agent_version=agent.version
            )
            print(f"  Cleaned up agent: {agent.name}")

            print(f"\n✓ TEST PASSED: Fabric Data Agent via {label}")
            return True

    except Exception as e:
        error_str = str(e)
        print(f"\n✗ TEST FAILED: {error_str}")
        log_exception_info(e, "Fabric Tool Error")

        if "TaskCanceledException" in error_str:
            print("\n  ⚠ Known Issue: TaskCanceledException")
            print("  This occurs when request hits the wrong Hyena scale unit")
            print("  (Data Proxy is only deployed on one of two scale units)")
            print("  Re-running the test may succeed on the next attempt.")
        elif "424" in error_str or "Failed Dependency" in error_str:
            print("\n  ⚠ Known Issue: DNS Resolution / VNet Routing")
            print("  Data Proxy cannot resolve private Fabric workspace endpoint.")
        elif "http_client_error" in error_str:
            print("\n  ⚠ Possible VNet Routing Issue")
            print("  The Fabric tool executor may not have VNet DNS access.")
            print("  This is similar to the known OpenAPI tool VNet issue.")

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
        description="Fabric Data Agent Test Script",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  python test_fabric_data_agent_v2.py                    # Run all tests
  python test_fabric_data_agent_v2.py --test public      # Only public workspace tests
  python test_fabric_data_agent_v2.py --test private     # Only private workspace tests
  python test_fabric_data_agent_v2.py --retry 3          # Retry failed agent tests

Environment variables:
  PROJECT_ENDPOINT              - Azure AI project endpoint
  MODEL_NAME                    - Model to use (default: gpt-4o-mini)
  FABRIC_CONNECTION_ID_PUBLIC   - Project connection ID for public Fabric workspace
  FABRIC_CONNECTION_ID_PRIVATE  - Project connection ID for private Fabric workspace (VNet)
  FABRIC_TEST_QUERY             - Custom query to test (default: asks about available data)
""",
    )
    parser.add_argument(
        "--test",
        choices=["public", "private", "all"],
        default="all",
        help="Which Fabric workspace to test: public, private, or all (default: all)",
    )
    parser.add_argument(
        "--retry",
        type=int,
        default=1,
        help="Number of retries for agent tests (default: 1)",
    )
    parser.add_argument(
        "--query",
        type=str,
        default=None,
        help="Custom query to send to the Fabric data agent",
    )
    args = parser.parse_args()

    global FABRIC_TEST_QUERY
    if args.query:
        FABRIC_TEST_QUERY = args.query

    print("=" * 60)
    print("FABRIC DATA AGENT TEST")
    print("=" * 60)
    print("\nConfiguration:")
    print(f"  Project Endpoint: {PROJECT_ENDPOINT}")
    print(f"  Model: {MODEL_NAME}")
    print(f"  Public Fabric Connection: {FABRIC_CONNECTION_ID_PUBLIC or '(not set)'}")
    print(f"  Private Fabric Connection: {FABRIC_CONNECTION_ID_PRIVATE or '(not set)'}")
    print(f"  Test Query: {FABRIC_TEST_QUERY}")

    results = {}

    # Connectivity tests
    if args.test in ["public", "all"] and FABRIC_CONNECTION_ID_PUBLIC:
        result = test_fabric_connectivity(
            FABRIC_CONNECTION_ID_PUBLIC, "Public Fabric Workspace"
        )
        if result is not None:
            results["connectivity_public"] = result

    if args.test in ["private", "all"] and FABRIC_CONNECTION_ID_PRIVATE:
        result = test_fabric_connectivity(
            FABRIC_CONNECTION_ID_PRIVATE, "Private Fabric Workspace"
        )
        if result is not None:
            results["connectivity_private"] = result

    # Agent tests: Fabric Data Agent (Public)
    if args.test in ["public", "all"] and FABRIC_CONNECTION_ID_PUBLIC:
        for attempt in range(args.retry):
            if attempt > 0:
                print(f"\n  Retry attempt {attempt + 1}/{args.retry}...")
            result = test_fabric_tool_via_agent(
                FABRIC_CONNECTION_ID_PUBLIC, "Public Fabric Workspace"
            )
            if result is not None:
                results["agent_public"] = result
                if result:
                    break
        else:
            if "agent_public" not in results:
                results["agent_public"] = False

    # Agent tests: Fabric Data Agent (Private / VNet)
    if args.test in ["private", "all"] and FABRIC_CONNECTION_ID_PRIVATE:
        for attempt in range(args.retry):
            if attempt > 0:
                print(f"\n  Retry attempt {attempt + 1}/{args.retry}...")
            result = test_fabric_tool_via_agent(
                FABRIC_CONNECTION_ID_PRIVATE, "Private Fabric Workspace"
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
            "  No tests were run. Set FABRIC_CONNECTION_ID_PUBLIC and/or FABRIC_CONNECTION_ID_PRIVATE."
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
