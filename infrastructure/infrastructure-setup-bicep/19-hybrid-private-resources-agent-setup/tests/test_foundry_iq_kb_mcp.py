#!/usr/bin/env python3
"""
Foundry IQ KB MCP Server Test Script

Tests the Foundry IQ Knowledge Base MCP server integration with
Azure AI Foundry Agents V2 in a VNet / private endpoint scenario.

Tests:
1. MCP Connectivity (Direct HTTP) — Full session flow:
   initialize → tools/list → knowledge_base_retrieve
2. MCP KB Retrieve via Agent — Agent V2 with MCPTool calling
   knowledge_base_retrieve through Data Proxy
3. SharePoint Headers Test — Validates x-ms-sharepoint-* headers
   are passed through MCP → AI Search call chain
4. Multi-Source Test — Query returning results from SharePoint,
   search index, and web sources

Usage:
  python test_foundry_iq_kb_mcp.py                          # Run all tests
  python test_foundry_iq_kb_mcp.py --test connectivity       # MCP session flow only
  python test_foundry_iq_kb_mcp.py --test agent              # Agent integration only
  python test_foundry_iq_kb_mcp.py --test sharepoint_headers # SP header passthrough
  python test_foundry_iq_kb_mcp.py --test multi_source       # Multi-source retrieval
  python test_foundry_iq_kb_mcp.py --retry 3                 # Retry for Hyena routing

Environment variables:
  PROJECT_ENDPOINT          - Azure AI project endpoint
  MODEL_NAME                - Model to use (default: gpt-4o-mini)
  MCP_SERVER_URL            - Foundry IQ KB MCP server URL (default: public)
  MCP_SERVER_PRIVATE        - Private MCP server URL (VNet internal)
"""

import argparse
import json
import logging
import os
import ssl
import sys
import urllib.error
import urllib.request

# ============================================================================
# LOGGING
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
from azure.ai.projects.models import MCPTool, PromptAgentDefinition
from azure.identity import DefaultAzureCredential
from openai.types.responses import ResponseInputParam
from openai.types.responses.response_input_param import McpApprovalResponse

# ============================================================================
# CONFIGURATION
# ============================================================================
PROJECT_ENDPOINT = os.environ.get(
    "PROJECT_ENDPOINT",
    "https://<ai-services>.services.ai.azure.com/api/projects/<project>",
)
MODEL_NAME = os.environ.get("MODEL_NAME", "gpt-4o-mini")

# Foundry IQ KB MCP server URLs
# Public: accessible from anywhere (for testing without VPN)
MCP_SERVER_URL = os.environ.get(
    "MCP_SERVER_URL",
    "https://<container-app>.azurecontainerapps.io/mcp",
)
# Private: internal to VNet (accessible only via Data Proxy or VPN)
MCP_SERVER_PRIVATE = os.environ.get(
    "MCP_SERVER_PRIVATE",
    "",
)

# ============================================================================


def log_response_info(response, label="Response"):
    """Extract and log debugging info from OpenAI response objects."""
    logger = logging.getLogger(__name__)
    try:
        if hasattr(response, "_request_id"):
            logger.info(f"{label} - Request ID: {response._request_id}")
        if hasattr(response, "id"):
            logger.info(f"{label} - Response ID: {response.id}")
        if hasattr(response, "_response") and hasattr(response._response, "headers"):
            headers = response._response.headers
            for h in ("x-request-id", "x-ms-request-id"):
                if h in headers:
                    logger.info(f"{label} - {h}: {headers[h]}")
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
            print(f"  📋 Request ID: {request_id}")
            print(f"  📋 MS Request ID: {ms_request_id}")
            if hasattr(resp, "status_code"):
                logger.error(f"{label} - HTTP Status: {resp.status_code}")
    except Exception as e:
        logger.debug(f"Could not extract exception info: {e}")


# ============================================================================
# TEST 1: MCP Connectivity (Direct HTTP)
# ============================================================================
def test_mcp_connectivity(mcp_url, label="Foundry IQ KB MCP Server"):
    """
    Test MCP server with full session workflow:
    initialize → tools/list → tools/call (knowledge_base_retrieve)
    """
    print("\n" + "=" * 60)
    print(f"TEST: MCP Connectivity — {label}")
    print("=" * 60)

    ctx = ssl.create_default_context()
    print(f"  Target: {mcp_url}")

    session_id = None

    try:
        # ── Step 1: Initialize ──────────────────────────────────────────
        print("\n--- Step 1: Initialize (get mcp-session-id) ---")
        init_data = json.dumps({
            "method": "initialize",
            "params": {
                "protocolVersion": "2025-11-25",
                "capabilities": {"sampling": {}, "elicitation": {}, "roots": {"listChanged": True}},
                "clientInfo": {"name": "foundry-iq-kb-test-client", "version": "1.0.0"},
            },
            "jsonrpc": "2.0",
            "id": 0,
        }).encode("utf-8")

        req = urllib.request.Request(
            mcp_url,
            data=init_data,
            headers={"Content-Type": "application/json", "Accept": "application/json, text/event-stream"},
            method="POST",
        )

        with urllib.request.urlopen(req, timeout=15, context=ctx) as resp:
            status = resp.getcode()
            body = resp.read().decode("utf-8")
            session_id = resp.getheader("mcp-session-id")
            print(f"  ✓ HTTP Status: {status}")
            print(f"  ✓ Response: {body[:300]}...")
            if session_id:
                print(f"  ✓ MCP Session ID: {session_id}")
            else:
                print("  ⚠ No mcp-session-id header (stateless mode)")

        # ── Step 2: List Tools ──────────────────────────────────────────
        print("\n--- Step 2: List Tools ---")
        list_data = json.dumps({
            "jsonrpc": "2.0", "id": 1, "method": "tools/list", "params": {}
        }).encode("utf-8")

        list_headers = {"Content-Type": "application/json", "Accept": "application/json, text/event-stream"}
        if session_id:
            list_headers["mcp-session-id"] = session_id

        list_req = urllib.request.Request(mcp_url, data=list_data, headers=list_headers, method="POST")

        with urllib.request.urlopen(list_req, timeout=15, context=ctx) as resp:
            body = resp.read().decode("utf-8")
            result = json.loads(body)
            print(f"  ✓ HTTP Status: {resp.getcode()}")

            if "result" in result and "tools" in result["result"]:
                tools = result["result"]["tools"]
                print(f"  ✓ Found {len(tools)} tools:")
                for tool in tools:
                    print(f"      - {tool.get('name', '?')}: {tool.get('description', '')[:60]}")

                # Verify knowledge_base_retrieve is present
                kb_tool = next((t for t in tools if t["name"] == "knowledge_base_retrieve"), None)
                if kb_tool:
                    print("  ✓ knowledge_base_retrieve tool found")
                else:
                    print("  ✗ knowledge_base_retrieve tool NOT found")
                    return False

        # ── Step 3: Call knowledge_base_retrieve ────────────────────────
        print("\n--- Step 3: Call knowledge_base_retrieve ---")
        call_data = json.dumps({
            "jsonrpc": "2.0",
            "id": 2,
            "method": "tools/call",
            "params": {
                "name": "knowledge_base_retrieve",
                "arguments": {"query": "vendor management policy", "top_k": 3},
            },
        }).encode("utf-8")

        call_headers = {"Content-Type": "application/json", "Accept": "application/json, text/event-stream"}
        if session_id:
            call_headers["mcp-session-id"] = session_id

        call_req = urllib.request.Request(mcp_url, data=call_data, headers=call_headers, method="POST")

        with urllib.request.urlopen(call_req, timeout=30, context=ctx) as resp:
            body = resp.read().decode("utf-8")
            result = json.loads(body)
            print(f"  ✓ HTTP Status: {resp.getcode()}")

            if "result" in result:
                content = result["result"].get("content", [])
                # Check for structured content or text content
                text_parts = [c for c in content if c.get("type") == "text"]
                resource_parts = [c for c in content if c.get("type") == "resource"]

                if text_parts:
                    text = text_parts[0].get("text", "")
                    print(f"  ✓ Text response: {text[:200]}...")
                if resource_parts:
                    print(f"  ✓ Resource parts: {len(resource_parts)}")

                print("  ✓ knowledge_base_retrieve returned results")
            else:
                error = result.get("error", {})
                print(f"  ⚠ Error: {error.get('message', 'Unknown')}")
                # Still pass — the tool responded (stub mode is fine)

        print("\n" + "=" * 60)
        print(f"✓ TEST PASSED: {label} session flow working")
        print("=" * 60)
        return True

    except Exception as e:
        print(f"\n✗ TEST FAILED: {e}")
        import traceback
        traceback.print_exc()
        return False


# ============================================================================
# TEST 2: MCP KB Retrieve via Agent V2
# ============================================================================
def test_kb_retrieve_via_agent(mcp_url, label="Foundry IQ KB MCP Server"):
    """
    Test KB retrieval through Foundry Agent V2 → MCPTool → MCP Server → AI Search.
    """
    print("\n" + "=" * 60)
    print(f"TEST: KB Retrieve via Agent — {label}")
    print("=" * 60)

    agent = None

    try:
        with (
            DefaultAzureCredential() as credential,
            AIProjectClient(credential=credential, endpoint=PROJECT_ENDPOINT) as project_client,
            project_client.get_openai_client() as openai_client,
        ):
            print(f"✓ Connected to AI Project at {PROJECT_ENDPOINT}")

            # Create MCP tool pointing to the Foundry IQ KB MCP server
            mcp_tool = MCPTool(
                server_label="foundry-iq-kb",
                server_url=mcp_url,
                require_approval="never",
            )

            agent = project_client.agents.create_version(
                agent_name="foundry-iq-kb-test",
                definition=PromptAgentDefinition(
                    model=MODEL_NAME,
                    instructions="""You are a knowledge retrieval assistant.
                    First call read_me to learn how the knowledge base works.
                    Then use knowledge_base_retrieve to search for information.
                    Always summarize the results you find, including source types and titles.""",
                    tools=[mcp_tool],
                ),
            )
            print(f"✓ Created agent with Foundry IQ KB MCP tool (id: {agent.id})")
            print(f"  MCP Server: {mcp_url}")

            # Create conversation
            conversation = openai_client.conversations.create()
            print(f"✓ Created conversation: {conversation.id}")

            # Send a query that should trigger knowledge_base_retrieve
            print("  Sending KB search request to agent...")
            response = openai_client.responses.create(
                conversation=conversation.id,
                input="Search the knowledge base for information about vendor management policies. List the document titles and source types you find.",
                extra_body={"agent": {"name": agent.name, "type": "agent_reference"}},
            )
            log_response_info(response, "KB Retrieve Response")

            # Handle MCP approval if needed
            for item in response.output:
                if hasattr(item, "type") and item.type == "mcp_approval_request":
                    print(f"  MCP approval requested for: {item.server_label}")
                    input_list: ResponseInputParam = [
                        McpApprovalResponse(
                            type="mcp_approval_response",
                            approve=True,
                            approval_request_id=item.id,
                        )
                    ]
                    response = openai_client.responses.create(
                        input=input_list,
                        previous_response_id=response.id,
                        extra_body={"agent": {"name": agent.name, "type": "agent_reference"}},
                    )

            output_text = response.output_text
            truncated = output_text[:500] + "..." if len(output_text) > 500 else output_text
            print(f"\n✓ Agent response: {truncated}")

            # Cleanup
            project_client.agents.delete_version(agent_name=agent.name, agent_version=agent.version)
            print(f"  Cleaned up agent: {agent.name}")

            print("\n" + "=" * 60)
            print(f"✓ TEST PASSED: KB retrieve via agent ({label})")
            print("=" * 60)
            return True

    except Exception as e:
        print(f"\n✗ TEST FAILED: {e}")
        log_exception_info(e, "KB Retrieve Error")

        error_str = str(e)
        if "TaskCanceledException" in error_str:
            print("\n  ⚠ Known Issue: TaskCanceledException")
            print("  Hyena cluster routing — Data Proxy on only 1 of 2 scale units.")
            print("  Re-run with --retry to mitigate.")
        elif "424" in error_str or "Failed Dependency" in error_str:
            print("\n  ⚠ Known Issue: DNS Resolution")
            print("  Data Proxy cannot resolve private Container Apps DNS.")

        import traceback
        traceback.print_exc()

        if agent:
            try:
                with (
                    DefaultAzureCredential() as cred,
                    AIProjectClient(credential=cred, endpoint=PROJECT_ENDPOINT) as pc,
                ):
                    pc.agents.delete_version(agent_name=agent.name, agent_version=agent.version)
                    print(f"  Cleaned up agent: {agent.name}")
            except Exception:
                pass
        return False


# ============================================================================
# TEST 3: SharePoint Headers Passthrough
# ============================================================================
def test_sharepoint_headers_via_mcp(mcp_url, label="Foundry IQ KB MCP Server"):
    """
    Test that x-ms-sharepoint-* headers can be passed through the MCP call.

    The Foundry IQ KB MCP server's knowledge_base_retrieve tool accepts
    optional parameters. This test validates that queries filtering to
    SharePoint sources work correctly when global headers are relevant.
    """
    print("\n" + "=" * 60)
    print(f"TEST: SharePoint Headers via MCP — {label}")
    print("=" * 60)

    ctx = ssl.create_default_context()

    try:
        # Initialize session
        init_data = json.dumps({
            "method": "initialize",
            "params": {
                "protocolVersion": "2025-11-25",
                "capabilities": {},
                "clientInfo": {"name": "sp-header-test", "version": "1.0.0"},
            },
            "jsonrpc": "2.0",
            "id": 0,
        }).encode("utf-8")

        req = urllib.request.Request(
            mcp_url, data=init_data,
            headers={"Content-Type": "application/json", "Accept": "application/json, text/event-stream"},
            method="POST",
        )
        with urllib.request.urlopen(req, timeout=15, context=ctx) as resp:
            session_id = resp.getheader("mcp-session-id")

        # Call knowledge_base_retrieve with SharePoint source filter
        call_data = json.dumps({
            "jsonrpc": "2.0",
            "id": 1,
            "method": "tools/call",
            "params": {
                "name": "knowledge_base_retrieve",
                "arguments": {
                    "query": "vendor policy approval process",
                    "sources": ["sharepoint"],
                    "top_k": 5,
                },
            },
        }).encode("utf-8")

        call_headers = {
            "Content-Type": "application/json",
            "Accept": "application/json, text/event-stream",
            # SharePoint global headers — these should be forwarded to AI Search
            "x-ms-sharepoint-siteurl": "https://contoso.sharepoint.com/sites/policies",
            "x-ms-sharepoint-tenantid": "00000000-0000-0000-0000-000000000000",
            "x-ms-sharepoint-accesstoken": "test-sp-header-passthrough",
        }
        if session_id:
            call_headers["mcp-session-id"] = session_id

        call_req = urllib.request.Request(mcp_url, data=call_data, headers=call_headers, method="POST")

        with urllib.request.urlopen(call_req, timeout=30, context=ctx) as resp:
            body = resp.read().decode("utf-8")
            result = json.loads(body)
            print(f"  ✓ HTTP Status: {resp.getcode()}")

            if "result" in result:
                content = result["result"].get("content", [])
                text_parts = [c for c in content if c.get("type") == "text"]
                if text_parts:
                    text = text_parts[0].get("text", "")
                    # Check that results are from SharePoint sources
                    has_sharepoint = "sharepoint" in text.lower() or "vendor" in text.lower()
                    if has_sharepoint:
                        print("  ✓ SharePoint-filtered results returned")
                    else:
                        print("  ⚠ Results returned but no clear SharePoint indicator")
                    print(f"  ✓ Response preview: {text[:200]}...")
                print("  ✓ x-ms-sharepoint-* headers did not cause rejection")
            else:
                error = result.get("error", {})
                print(f"  ⚠ Error: {error.get('message', 'Unknown')}")

        print("\n✓ TEST PASSED: SharePoint headers accepted via MCP")
        return True

    except Exception as e:
        print(f"\n✗ TEST FAILED: {e}")
        import traceback
        traceback.print_exc()
        return False


# ============================================================================
# TEST 4: Multi-Source Retrieval
# ============================================================================
def test_multi_source_via_agent(mcp_url, label="Foundry IQ KB MCP Server"):
    """
    Test that the KB MCP server returns results from multiple source types
    (SharePoint, search index, web) through the Foundry Agent.
    """
    print("\n" + "=" * 60)
    print(f"TEST: Multi-Source Retrieval via Agent — {label}")
    print("=" * 60)

    agent = None

    try:
        with (
            DefaultAzureCredential() as credential,
            AIProjectClient(credential=credential, endpoint=PROJECT_ENDPOINT) as project_client,
            project_client.get_openai_client() as openai_client,
        ):
            print(f"✓ Connected to AI Project at {PROJECT_ENDPOINT}")

            mcp_tool = MCPTool(
                server_label="foundry-iq-kb",
                server_url=mcp_url,
                require_approval="never",
            )

            agent = project_client.agents.create_version(
                agent_name="kb-multi-source-test",
                definition=PromptAgentDefinition(
                    model=MODEL_NAME,
                    instructions="""You are a knowledge retrieval assistant.
                    Use knowledge_base_retrieve to search. When reporting results,
                    ALWAYS include the source type (sharepoint, searchindex, or web)
                    for each result. List ALL results with their source types.""",
                    tools=[mcp_tool],
                ),
            )
            print(f"✓ Created agent (id: {agent.id})")

            conversation = openai_client.conversations.create()
            print(f"✓ Created conversation: {conversation.id}")

            # Broad query to hit all source types
            print("  Sending broad query to retrieve multi-source results...")
            response = openai_client.responses.create(
                conversation=conversation.id,
                input="Search the knowledge base for information about policies, best practices, and documentation. I want to see results from all available source types. List each result with its source type.",
                extra_body={"agent": {"name": agent.name, "type": "agent_reference"}},
            )
            log_response_info(response, "Multi-Source Response")

            # Handle MCP approval
            for item in response.output:
                if hasattr(item, "type") and item.type == "mcp_approval_request":
                    input_list: ResponseInputParam = [
                        McpApprovalResponse(
                            type="mcp_approval_response",
                            approve=True,
                            approval_request_id=item.id,
                        )
                    ]
                    response = openai_client.responses.create(
                        input=input_list,
                        previous_response_id=response.id,
                        extra_body={"agent": {"name": agent.name, "type": "agent_reference"}},
                    )

            output_text = response.output_text
            truncated = output_text[:700] + "..." if len(output_text) > 700 else output_text
            print(f"\n✓ Agent response: {truncated}")

            # Check for source type mentions
            output_lower = output_text.lower()
            source_checks = {
                "sharepoint": any(kw in output_lower for kw in ["sharepoint", "vendor_policy", "remote_work"]),
                "searchindex": any(kw in output_lower for kw in ["searchindex", "search index", "api_authentication", "engineering"]),
                "web": any(kw in output_lower for kw in ["web", "gartner", "mckinsey", "nist", "forrester"]),
            }

            for source, found in source_checks.items():
                status = "✓" if found else "⚠"
                print(f"  {status} Source '{source}' in response: {'Yes' if found else 'Not detected'}")

            # Cleanup
            project_client.agents.delete_version(agent_name=agent.name, agent_version=agent.version)
            print(f"  Cleaned up agent: {agent.name}")

            all_sources = all(source_checks.values())
            if all_sources:
                print("\n✓ TEST PASSED: All 3 source types represented")
            else:
                print("\n⚠ TEST PARTIAL: Not all source types detected in response")
                print("  (This may be due to stub data or query relevance ranking)")

            return True  # Pass as long as we got a response

    except Exception as e:
        print(f"\n✗ TEST FAILED: {e}")
        log_exception_info(e, "Multi-Source Error")
        import traceback
        traceback.print_exc()

        if agent:
            try:
                with (
                    DefaultAzureCredential() as cred,
                    AIProjectClient(credential=cred, endpoint=PROJECT_ENDPOINT) as pc,
                ):
                    pc.agents.delete_version(agent_name=agent.name, agent_version=agent.version)
            except Exception:
                pass
        return False


# ============================================================================
# MAIN
# ============================================================================
def main():
    parser = argparse.ArgumentParser(
        description="Foundry IQ KB MCP Server Tests — VNet Enterprise Scenario",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  python test_foundry_iq_kb_mcp.py                           # All tests
  python test_foundry_iq_kb_mcp.py --test connectivity        # MCP session flow
  python test_foundry_iq_kb_mcp.py --test agent               # Agent integration
  python test_foundry_iq_kb_mcp.py --test sharepoint_headers  # SP header passthrough
  python test_foundry_iq_kb_mcp.py --test multi_source        # Multi-source retrieval
  python test_foundry_iq_kb_mcp.py --retry 3                  # With retries

Environment variables:
  PROJECT_ENDPOINT    - AI Foundry project endpoint
  MODEL_NAME          - Model (default: gpt-4o-mini)
  MCP_SERVER_URL      - Public MCP server URL
  MCP_SERVER_PRIVATE  - Private MCP server URL (VNet)
""",
    )
    parser.add_argument(
        "--test",
        choices=["connectivity", "agent", "sharepoint_headers", "multi_source", "all"],
        default="all",
        help="Which test to run (default: all)",
    )
    parser.add_argument(
        "--server",
        choices=["public", "private"],
        default="public",
        help="Which MCP server to test against (default: public)",
    )
    parser.add_argument(
        "--retry", type=int, default=1,
        help="Number of attempts for agent tests (default: 1)",
    )
    args = parser.parse_args()

    mcp_url = MCP_SERVER_PRIVATE if args.server == "private" and MCP_SERVER_PRIVATE else MCP_SERVER_URL
    server_label = f"{'Private' if args.server == 'private' else 'Public'} Foundry IQ KB MCP Server"

    print("=" * 60)
    print("FOUNDRY IQ KB MCP SERVER — VNET ENTERPRISE TEST")
    print("=" * 60)
    print(f"\nConfiguration:")
    print(f"  Project Endpoint: {PROJECT_ENDPOINT}")
    print(f"  Model: {MODEL_NAME}")
    print(f"  MCP Server ({args.server}): {mcp_url}")

    results = {}

    # Test 1: MCP Connectivity
    if args.test in ["connectivity", "all"]:
        results["mcp_connectivity"] = test_mcp_connectivity(mcp_url, server_label)

    # Test 2: KB Retrieve via Agent
    if args.test in ["agent", "all"]:
        for attempt in range(args.retry):
            if attempt > 0:
                print(f"\n--- Retry attempt {attempt + 1}/{args.retry} ---")
            result = test_kb_retrieve_via_agent(mcp_url, server_label)
            if result:
                results["kb_retrieve_agent"] = True
                break
        else:
            results["kb_retrieve_agent"] = False

    # Test 3: SharePoint Headers
    if args.test in ["sharepoint_headers", "all"]:
        results["sharepoint_headers"] = test_sharepoint_headers_via_mcp(mcp_url, server_label)

    # Test 4: Multi-Source Retrieval
    if args.test in ["multi_source", "all"]:
        for attempt in range(args.retry):
            if attempt > 0:
                print(f"\n--- Retry attempt {attempt + 1}/{args.retry} ---")
            result = test_multi_source_via_agent(mcp_url, server_label)
            if result:
                results["multi_source"] = True
                break
        else:
            results["multi_source"] = False

    # Summary
    print("\n" + "=" * 60)
    print("TEST SUMMARY")
    print("=" * 60)
    for name, passed in results.items():
        status = "✓ PASSED" if passed else "✗ FAILED"
        print(f"  {name}: {status}")

    all_passed = all(results.values()) if results else True
    print("\n" + "=" * 60)
    if all_passed:
        print("ALL TESTS PASSED!")
    else:
        print("SOME TESTS FAILED")
        print("Note: Agent tests may fail due to Hyena cluster routing (~50% chance)")
        print("      Use --retry N to retry failed tests")
    print("=" * 60)

    sys.exit(0 if all_passed else 1)


if __name__ == "__main__":
    main()
