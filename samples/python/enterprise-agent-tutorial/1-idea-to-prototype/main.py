#!/usr/bin/env python3
"""
Microsoft Foundry Agent Sample - Tutorial 1: Modern Workplace Assistant

This sample demonstrates a complete business scenario using the Microsoft Foundry SDK:
- Agent creation with PromptAgentDefinition
- Conversation management via the Responses API
- Robust error handling and graceful degradation

Educational Focus:
- Enterprise AI patterns with the Microsoft Foundry SDK
- Real-world business scenarios that enterprises face daily
- Production-ready error handling and diagnostics
- Foundation for governance, evaluation, and monitoring (Tutorials 2-3)

Business Scenario:
An employee needs to implement Azure AD multi-factor authentication. They need:
1. Company security policy requirements
2. Technical implementation steps
3. Combined guidance showing how policy requirements map to technical implementation
"""

# <imports_and_includes>
import os
import time
from azure.ai.projects import AIProjectClient
from azure.ai.projects.models import (
    PromptAgentDefinition,
    SharepointPreviewTool,
    SharepointGroundingToolParameters,
    ToolProjectConnection,
    MCPTool,
)
from azure.identity import DefaultAzureCredential
from dotenv import load_dotenv
from openai.types.responses.response_input_param import (
    McpApprovalResponse,
)
# </imports_and_includes>

load_dotenv()

# ============================================================================
# AUTHENTICATION SETUP
# ============================================================================
endpoint = os.environ["PROJECT_ENDPOINT"]


def create_workplace_assistant(project_client):
    """
    Create a Modern Workplace Assistant using the Microsoft Foundry SDK.

    This demonstrates enterprise AI patterns:
    1. Agent creation with PromptAgentDefinition
    2. Robust error handling with graceful degradation
    3. Dynamic agent capabilities based on available resources
    4. Clear diagnostic information for troubleshooting

    Returns:
        agent: The created agent object
    """

    print("🤖 Creating Modern Workplace Assistant...")

    # ========================================================================
    # SHAREPOINT INTEGRATION SETUP
    # ========================================================================
    # <sharepoint_tool_setup>
    sharepoint_connection_id = os.environ.get("SHAREPOINT_CONNECTION_ID")
    sharepoint_tool = None

    if sharepoint_connection_id:
        print("📁 Configuring SharePoint integration...")
        print(f"   Connection ID: {sharepoint_connection_id}")

        try:
            sharepoint_tool = SharepointPreviewTool(
                sharepoint_grounding_preview=SharepointGroundingToolParameters(
                    project_connections=[
                        ToolProjectConnection(
                            project_connection_id=sharepoint_connection_id
                        )
                    ]
                )
            )
            print("✅ SharePoint tool configured successfully")
        except Exception as e:
            print(f"⚠️  SharePoint tool unavailable: {e}")
            print("   Agent will operate without SharePoint access")
            sharepoint_tool = None
    else:
        print("📁 SharePoint integration skipped (SHAREPOINT_CONNECTION_ID not set)")
    # </sharepoint_tool_setup>

    # ========================================================================
    # MICROSOFT LEARN MCP INTEGRATION SETUP
    # ========================================================================
    # <mcp_tool_setup>
    mcp_server_url = os.environ.get("MCP_SERVER_URL")
    mcp_tool = None

    if mcp_server_url:
        print("📚 Configuring Microsoft Learn MCP integration...")
        print(f"   Server URL: {mcp_server_url}")

        try:
            mcp_tool = MCPTool(
                server_url=mcp_server_url,
                server_label="Microsoft_Learn_Documentation",
                require_approval="always",
            )
            print("✅ MCP tool configured successfully")
        except Exception as e:
            print(f"⚠️  MCP tool unavailable: {e}")
            print("   Agent will operate without Microsoft Learn access")
            mcp_tool = None
    else:
        print("📚 MCP integration skipped (MCP_SERVER_URL not set)")
    # </mcp_tool_setup>

    # ========================================================================
    # AGENT CREATION WITH DYNAMIC CAPABILITIES
    # ========================================================================
    if sharepoint_tool and mcp_tool:
        instructions = """You are a Modern Workplace Assistant for Contoso Corporation.

CAPABILITIES:
- Search SharePoint for company policies, procedures, and internal documentation
- Access Microsoft Learn for current Azure and Microsoft 365 technical guidance
- Provide comprehensive solutions combining internal requirements with external implementation

RESPONSE STRATEGY:
- For policy questions: Search SharePoint for company-specific requirements and guidelines
- For technical questions: Use Microsoft Learn for current Azure/M365 documentation
- For implementation questions: Combine both sources to show how company policies map to technical implementation
- Always cite your sources and provide step-by-step guidance"""
    elif sharepoint_tool:
        instructions = """You are a Modern Workplace Assistant with access to Contoso Corporation's SharePoint.

CAPABILITIES:
- Search SharePoint for company policies, procedures, and internal documentation
- Provide detailed technical guidance based on your knowledge
- Combine company policies with general best practices"""
    elif mcp_tool:
        instructions = """You are a Technical Assistant with access to Microsoft Learn documentation.

CAPABILITIES:
- Access Microsoft Learn for current Azure and Microsoft 365 technical guidance
- Provide detailed implementation steps and best practices
- Explain Azure services, features, and configuration options"""
    else:
        instructions = """You are a Technical Assistant specializing in Azure and Microsoft 365 guidance.

CAPABILITIES:
- Provide detailed Azure and Microsoft 365 technical guidance
- Explain implementation steps and best practices
- Help with Azure AD, Conditional Access, MFA, and security configurations"""

    # <create_agent_with_tools>
    print(f"🛠️  Creating agent with model: {os.environ['MODEL_DEPLOYMENT_NAME']}")

    tools = []
    if sharepoint_tool:
        tools.append(sharepoint_tool)
        print("   ✓ SharePoint tool added")
    if mcp_tool:
        tools.append(mcp_tool)
        print("   ✓ MCP tool added")

    print(f"   Total tools: {len(tools)}")

    agent = project_client.agents.create_version(
        agent_name="Modern Workplace Assistant",
        definition=PromptAgentDefinition(
            model=os.environ["MODEL_DEPLOYMENT_NAME"],
            instructions=instructions,
            tools=tools if tools else None,
        ),
    )

    print(f"✅ Agent created successfully (name: {agent.name}, version: {agent.version})")
    return agent
    # </create_agent_with_tools>


def demonstrate_business_scenarios(agent, openai_client):
    """
    Demonstrate realistic business scenarios with the Microsoft Foundry SDK.

    This function showcases the practical value of the Modern Workplace Assistant
    by walking through scenarios that enterprise employees face regularly.
    """

    scenarios = [
        {
            "title": "📋 Company Policy Question (SharePoint Only)",
            "question": "What is Contoso's remote work policy?",
            "context": "Employee needs to understand company-specific remote work requirements",
            "learning_point": "SharePoint tool retrieves internal company policies",
        },
        {
            "title": "📚 Technical Documentation Question (MCP Only)",
            "question": (
                "According to Microsoft Learn, what is the correct way to implement "
                "Azure AD Conditional Access policies? Please include reference links "
                "to the official documentation."
            ),
            "context": "IT administrator needs authoritative Microsoft technical guidance",
            "learning_point": "MCP tool accesses Microsoft Learn for official documentation with links",
        },
        {
            "title": "🔄 Combined Implementation Question (SharePoint + MCP)",
            "question": (
                "Based on our company's remote work security policy, how should I configure "
                "my Azure environment to comply? Please include links to Microsoft "
                "documentation showing how to implement each requirement."
            ),
            "context": "Need to map company policy to technical implementation with official guidance",
            "learning_point": "Both tools work together: SharePoint for policy + MCP for implementation docs",
        },
    ]

    print("\n" + "=" * 70)
    print("🏢 MODERN WORKPLACE ASSISTANT - BUSINESS SCENARIO DEMONSTRATION")
    print("=" * 70)
    print("This demonstration shows how AI agents solve real business problems")
    print("using the Microsoft Foundry SDK.")
    print("=" * 70)

    for i, scenario in enumerate(scenarios, 1):
        print(f"\n📊 SCENARIO {i}/3: {scenario['title']}")
        print("-" * 50)
        print(f"❓ QUESTION: {scenario['question']}")
        print(f"🎯 BUSINESS CONTEXT: {scenario['context']}")
        print(f"🎓 LEARNING POINT: {scenario['learning_point']}")
        print("-" * 50)

        # <agent_conversation>
        print("🤖 AGENT RESPONSE:")
        response, status = create_agent_response(agent, scenario["question"], openai_client)
        # </agent_conversation>

        if status == "completed" and response and len(response.strip()) > 10:
            print(f"✅ SUCCESS: {response[:300]}...")
            if len(response) > 300:
                print(f"   📏 Full response: {len(response)} characters")
        else:
            print(f"⚠️  RESPONSE: {response}")

        print(f"📈 STATUS: {status}")
        print("-" * 50)

        time.sleep(1)

    print("\n✅ DEMONSTRATION COMPLETED!")
    print("🎓 Key Learning Outcomes:")
    print("   • Microsoft Foundry SDK usage for enterprise AI")
    print("   • Conversation management via the Responses API")
    print("   • Real business value through AI assistance")
    print("   • Foundation for governance and monitoring (Tutorials 2-3)")

    return True


def create_agent_response(agent, message, openai_client):
    """
    Create a response from the workplace agent using the Responses API.

    This function demonstrates the response pattern for the Microsoft Foundry SDK
    including MCP tool approval handling.

    Args:
        agent: The agent object (with .name attribute)
        message: The user's message

    Returns:
        tuple: (response_text, status)
    """

    try:
        response = openai_client.responses.create(
            input=message,
            extra_body={
                "agent": {"name": agent.name, "type": "agent_reference"}
            },
        )

        # Handle MCP approval requests if present
        approval_list = []
        for item in response.output:
            if item.type == "mcp_approval_request" and item.id:
                approval_list.append(
                    McpApprovalResponse(
                        type="mcp_approval_response",
                        approve=True,
                        approval_request_id=item.id,
                    )
                )

        if approval_list:
            response = openai_client.responses.create(
                input=approval_list,
                previous_response_id=response.id,
                extra_body={
                    "agent": {"name": agent.name, "type": "agent_reference"}
                },
            )

        return response.output_text, "completed"

    except Exception as e:
        return f"Error in conversation: {str(e)}", "failed"


def interactive_mode(agent, openai_client):
    """Interactive mode for testing the workplace agent."""

    print("\n" + "=" * 60)
    print("💬 INTERACTIVE MODE - Test Your Workplace Agent!")
    print("=" * 60)
    print("Ask questions about Azure, M365, security, and technical implementation.")
    print("Type 'quit' to exit.")
    print("-" * 60)

    while True:
        try:
            question = input("\n❓ Your question: ").strip()

            if question.lower() in ["quit", "exit", "bye"]:
                break

            if not question:
                print("💡 Please ask a question about Azure or M365 technical implementation.")
                continue

            print("\n🤖 Workplace Agent: ", end="", flush=True)
            response, status = create_agent_response(agent, question, openai_client)
            print(response)

            if status != "completed":
                print(f"\n⚠️  Response status: {status}")

            print("-" * 60)

        except KeyboardInterrupt:
            break
        except Exception as e:
            print(f"\n❌ Error: {e}")
            print("-" * 60)

    print("\n👋 Thank you for testing the Modern Workplace Agent!")


def main():
    """Main execution flow demonstrating the complete sample."""

    print("🚀 Foundry - Modern Workplace Assistant")
    print("Tutorial 1: Building Enterprise Agents with Microsoft Foundry SDK")
    print("=" * 70)

    # <agent_authentication>
    with (
        DefaultAzureCredential() as credential,
        AIProjectClient(endpoint=endpoint, credential=credential) as project_client,
        project_client.get_openai_client() as openai_client,
    ):
        print(f"✅ Connected to Foundry: {endpoint}")
    # </agent_authentication>

        try:
            agent = create_workplace_assistant(project_client)
            demonstrate_business_scenarios(agent, openai_client)

            print("\n🎯 Try interactive mode? (y/n): ", end="")
            try:
                if input().lower().startswith("y"):
                    interactive_mode(agent, openai_client)
            except EOFError:
                print("n")

            print("\n🎉 Sample completed successfully!")
            print("📚 This foundation supports Tutorial 2 (Governance) and Tutorial 3 (Production)")
            print("🔗 Next: Add evaluation metrics, monitoring, and production deployment")

        except Exception as e:
            print(f"\n❌ Error: {e}")
            print("Please check your .env configuration and ensure:")
            print("  - PROJECT_ENDPOINT is correct")
            print("  - MODEL_DEPLOYMENT_NAME is deployed")
            print("  - Azure credentials are configured (az login)")


if __name__ == "__main__":
    main()
