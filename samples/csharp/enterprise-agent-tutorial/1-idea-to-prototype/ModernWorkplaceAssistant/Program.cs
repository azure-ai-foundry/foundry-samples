// <imports_and_includes>
using System;
using System.ClientModel;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Azure.AI.Projects;
using Azure.AI.Projects.OpenAI;
using Azure.Identity;
using DotNetEnv;
using OpenAI.Responses;
// </imports_and_includes>

#pragma warning disable OPENAI001

/*
 * Azure AI Foundry Agent Sample - Tutorial 1: Modern Workplace Assistant (C#)
 * 
 * This sample demonstrates a complete business scenario using the Azure AI Projects v2 SDK:
 * - Agent creation with PromptAgentDefinition and AgentVersion
 * - Conversation via the Responses API (ProjectResponsesClient)
 * - SharePoint and MCP tool integration on the agent definition
 * - MCP tool approval handling through the Responses API approval loop
 * - Robust error handling and graceful degradation
 * 
 * Educational Focus:
 * - Enterprise AI patterns with the v2 Azure AI Projects SDK
 * - Real-world business scenarios that enterprises face daily
 * - Production-ready error handling and diagnostics
 * - Foundation for governance, evaluation, and monitoring (Tutorials 2-3)
 * 
 * Business Scenario:
 * An employee needs to implement Azure AD multi-factor authentication. They need:
 * 1. Company security policy requirements (from SharePoint)
 * 2. Technical implementation steps (from Microsoft Learn via MCP)
 * 3. Combined guidance showing how policy requirements map to technical implementation
 */

class Program
{
    private static AIProjectClient? projectClient;
    private static ProjectResponsesClient? responseClient;
    private static string agentName = "Modern_Workplace_Assistant";

    static async Task Main(string[] args)
    {
        Console.WriteLine("🚀 Azure AI Foundry - Modern Workplace Assistant");
        Console.WriteLine("Tutorial 1: Building Enterprise Agents with SharePoint + MCP Tools");
        Console.WriteLine("".PadRight(70, '='));

        try
        {
            // Create the agent with full diagnostic output
            var agentVersion = await CreateWorkplaceAssistantAsync();

            // Demonstrate business scenarios
            await DemonstrateBusinessScenariosAsync(agentVersion);

            // Offer interactive testing
            Console.Write("\n🎯 Try interactive mode? (y/n): ");
            var response = Console.ReadLine();
            if (response?.ToLower().StartsWith("y") == true)
            {
                await InteractiveModeAsync(agentVersion);
            }

            // Cleanup
            Console.WriteLine("\n🧹 Cleaning up agent...");
            await projectClient!.Agents.DeleteAgentVersionAsync(
                agentName: agentVersion.Name,
                agentVersion: agentVersion.Version);
            Console.WriteLine("✅ Agent deleted");

            Console.WriteLine("\n🎉 Sample completed successfully!");
            Console.WriteLine("📚 This foundation supports Tutorial 2 (Governance) and Tutorial 3 (Production)");
            Console.WriteLine("🔗 Next: Add evaluation metrics, monitoring, and production deployment");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Error: {ex.Message}");
            Console.WriteLine("Please check your .env configuration and ensure:");
            Console.WriteLine("  - PROJECT_ENDPOINT is correct");
            Console.WriteLine("  - MODEL_DEPLOYMENT_NAME is deployed");
            Console.WriteLine("  - Azure credentials are configured (az login)");
            throw;
        }
    }

    /// <summary>
    /// Create a Modern Workplace Assistant with SharePoint and MCP tools.
    /// 
    /// This demonstrates enterprise AI patterns:
    /// 1. Agent creation with PromptAgentDefinition and CreateAgentVersionAsync
    /// 2. SharePoint integration via SharepointAgentTool
    /// 3. MCP integration via McpTool from the OpenAI Responses API
    /// 4. Robust error handling with graceful degradation
    /// 5. Dynamic agent capabilities based on available resources
    /// 
    /// Educational Value:
    /// - Shows real-world complexity of enterprise AI systems
    /// - Demonstrates how to handle partial system failures
    /// - Provides patterns for agent creation with multiple tools
    /// </summary>
    private static async Task<AgentVersion> CreateWorkplaceAssistantAsync()
    {
        // Load environment variables from shared .env file
        var envPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "shared", ".env");
        if (File.Exists(envPath))
        {
            Env.Load(envPath);
            Console.WriteLine($"📄 Loaded environment from: {envPath}");
        }
        else
        {
            // Fallback to local .env
            Env.Load(".env");
        }

        var projectEndpoint = Environment.GetEnvironmentVariable("PROJECT_ENDPOINT");
        var modelDeploymentName = Environment.GetEnvironmentVariable("MODEL_DEPLOYMENT_NAME");
        var sharePointConnectionName = Environment.GetEnvironmentVariable("SHAREPOINT_CONNECTION_NAME");
        var mcpServerUrl = Environment.GetEnvironmentVariable("MCP_SERVER_URL");

        if (string.IsNullOrEmpty(projectEndpoint))
            throw new InvalidOperationException("PROJECT_ENDPOINT environment variable not set");
        if (string.IsNullOrEmpty(modelDeploymentName))
            throw new InvalidOperationException("MODEL_DEPLOYMENT_NAME environment variable not set");

        Console.WriteLine("\n🤖 Creating Modern Workplace Assistant...");

        // ============================================================================
        // AUTHENTICATION SETUP
        // ============================================================================
        // <agent_authentication>
        var credential = new DefaultAzureCredential();

        projectClient = new AIProjectClient(new Uri(projectEndpoint), credential);
        Console.WriteLine($"✅ Connected to Azure AI Foundry: {projectEndpoint}");
        // </agent_authentication>

        // ========================================================================
        // SHAREPOINT INTEGRATION SETUP
        // ========================================================================
        // <sharepoint_connection_resolution>
        SharepointAgentTool? sharepointTool = null;

        if (!string.IsNullOrEmpty(sharePointConnectionName))
        {
            Console.WriteLine($"📁 Configuring SharePoint integration...");
            Console.WriteLine($"   Connection name: {sharePointConnectionName}");

            try
            {
                // <sharepoint_tool_setup>
                // Resolve connection name to connection ID via the Connections API
                AIProjectConnection sharepointConnection = await projectClient.Connections.GetConnectionAsync(
                    sharePointConnectionName, includeCredentials: false);

                SharePointGroundingToolOptions sharepointToolOption = new()
                {
                    ProjectConnections = { new ToolProjectConnection(projectConnectionId: sharepointConnection.Id) }
                };
                sharepointTool = new SharepointAgentTool(sharepointToolOption);
                Console.WriteLine($"✅ SharePoint tool configured successfully");
                // </sharepoint_tool_setup>
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️  SharePoint connection unavailable: {ex.Message}");
                Console.WriteLine($"   Possible causes:");
                Console.WriteLine($"   - Connection '{sharePointConnectionName}' doesn't exist in the project");
                Console.WriteLine($"   - Insufficient permissions to access the connection");
                Console.WriteLine($"   - Connection configuration is incomplete");
                Console.WriteLine($"   Agent will operate without SharePoint access");
            }
        }
        else
        {
            Console.WriteLine($"📁 SharePoint integration skipped (SHAREPOINT_CONNECTION_NAME not set)");
        }
        // </sharepoint_connection_resolution>

        // ========================================================================
        // MICROSOFT LEARN MCP INTEGRATION SETUP
        // ========================================================================
        // <mcp_tool_setup>
        // MCP (Model Context Protocol) enables agents to access external data sources
        // like Microsoft Learn documentation. The approval flow is handled in ChatWithAssistantAsync.
        McpTool? mcpTool = null;

        if (!string.IsNullOrEmpty(mcpServerUrl))
        {
            Console.WriteLine($"📚 Configuring Microsoft Learn MCP integration...");
            Console.WriteLine($"   Server URL: {mcpServerUrl}");

            try
            {
                // Create MCP tool for Microsoft Learn documentation access
                // server_label must match pattern: ^[a-zA-Z0-9_]+$ (alphanumeric and underscores only)
                mcpTool = new McpTool("Microsoft_Learn_Documentation", new Uri(mcpServerUrl));
                Console.WriteLine($"✅ MCP tool configured successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️  MCP tool unavailable: {ex.Message}");
                Console.WriteLine($"   Agent will operate without Microsoft Learn access");
            }
        }
        else
        {
            Console.WriteLine($"📚 MCP integration skipped (MCP_SERVER_URL not set)");
        }
        // </mcp_tool_setup>

        // ========================================================================
        // AGENT CREATION WITH DYNAMIC CAPABILITIES
        // ========================================================================
        // Create agent instructions based on available data sources
        string instructions = GetAgentInstructions(sharepointTool != null, mcpTool != null);

        // <create_agent_with_tools>
        // Create the agent using the v2 SDK with PromptAgentDefinition
        Console.WriteLine($"🛠️  Creating agent with model: {modelDeploymentName}");

        var agentDefinition = new PromptAgentDefinition(modelDeploymentName)
        {
            Instructions = instructions
        };

        // Add tools to the agent definition
        if (sharepointTool != null)
        {
            agentDefinition.Tools.Add(sharepointTool);
            Console.WriteLine($"   ✓ SharePoint tool added");
        }

        if (mcpTool != null)
        {
            agentDefinition.Tools.Add(mcpTool);
            Console.WriteLine($"   ✓ MCP tool added");
        }

        Console.WriteLine($"   Total tools: {agentDefinition.Tools.Count}");

        // Create agent version
        AgentVersion agentVersion = await projectClient.Agents.CreateAgentVersionAsync(
            agentName: agentName,
            options: new(agentDefinition));

        // Create a response client bound to this agent for conversations
        responseClient = projectClient.OpenAI
            .GetProjectResponsesClientForAgent(agentVersion);

        Console.WriteLine($"✅ Agent created successfully: {agentVersion.Name} (version {agentVersion.Version})");
        return agentVersion;
        // </create_agent_with_tools>
    }

    /// <summary>
    /// Generate agent instructions based on available tools.
    /// </summary>
    private static string GetAgentInstructions(bool hasSharePoint, bool hasMcp)
    {
        if (hasSharePoint && hasMcp)
        {
            return @"You are a Modern Workplace Assistant for Contoso Corporation.

CAPABILITIES:
- Search SharePoint for company policies, procedures, and internal documentation
- Access Microsoft Learn for current Azure and Microsoft 365 technical guidance
- Provide comprehensive solutions combining internal requirements with external implementation

RESPONSE STRATEGY:
- For policy questions: Search SharePoint for company-specific requirements and guidelines
- For technical questions: Use Microsoft Learn for current Azure/M365 documentation and best practices
- For implementation questions: Combine both sources to show how company policies map to technical implementation
- Always cite your sources and provide step-by-step guidance
- Explain how internal requirements connect to external implementation steps

EXAMPLE SCENARIOS:
- ""What is our MFA policy?"" → Search SharePoint for security policies
- ""How do I configure Azure AD Conditional Access?"" → Use Microsoft Learn for technical steps
- ""Our policy requires MFA - how do I implement this?"" → Combine policy requirements with implementation guidance";
        }
        else if (hasSharePoint)
        {
            return @"You are a Modern Workplace Assistant with access to Contoso Corporation's SharePoint.

CAPABILITIES:
- Search SharePoint for company policies, procedures, and internal documentation
- Provide detailed technical guidance based on your knowledge
- Combine company policies with general best practices

RESPONSE STRATEGY:
- Search SharePoint for company-specific requirements
- Provide technical guidance based on Azure and M365 best practices
- Explain how to align implementations with company policies";
        }
        else if (hasMcp)
        {
            return @"You are a Technical Assistant with access to Microsoft Learn documentation.

CAPABILITIES:
- Access Microsoft Learn for current Azure and Microsoft 365 technical guidance
- Provide detailed implementation steps and best practices
- Explain Azure services, features, and configuration options

RESPONSE STRATEGY:
- Use Microsoft Learn for technical documentation
- Provide comprehensive implementation guidance
- Reference official documentation and best practices";
        }
        else
        {
            return @"You are a Technical Assistant specializing in Azure and Microsoft 365 guidance.

CAPABILITIES:
- Provide detailed Azure and Microsoft 365 technical guidance
- Explain implementation steps and best practices
- Help with Azure AD, Conditional Access, MFA, and security configurations

RESPONSE STRATEGY:
- Provide comprehensive technical guidance
- Include step-by-step implementation instructions
- Reference best practices and security considerations";
        }
    }

    /// <summary>
    /// Demonstrate realistic business scenarios.
    /// 
    /// This function showcases the practical value of the Modern Workplace Assistant
    /// by walking through scenarios that enterprise employees face regularly.
    /// 
    /// Educational Value:
    /// - Shows real business problems that AI agents can solve
    /// - Demonstrates the Responses API conversation pattern
    /// - Illustrates conversation patterns with tool usage
    /// </summary>
    private static async Task DemonstrateBusinessScenariosAsync(AgentVersion agentVersion)
    {
        var scenarios = new[]
        {
            new
            {
                Title = "📋 Company Policy Question (SharePoint Only)",
                Question = "What is Contoso's remote work policy?",
                Context = "Employee needs to understand company-specific remote work requirements",
                LearningPoint = "SharePoint tool retrieves internal company policies"
            },
            new
            {
                Title = "📚 Technical Documentation Question (MCP Only)",
                Question = "According to Microsoft Learn, what is the correct way to implement Azure AD Conditional Access policies? Please include reference links to the official documentation.",
                Context = "IT administrator needs authoritative Microsoft technical guidance",
                LearningPoint = "MCP tool accesses Microsoft Learn for official documentation with links"
            },
            new
            {
                Title = "🔄 Combined Implementation Question (SharePoint + MCP)",
                Question = "Based on our company's remote work security policy, how should I configure my Azure environment to comply? Please include links to Microsoft documentation showing how to implement each requirement.",
                Context = "Need to map company policy to technical implementation with official guidance",
                LearningPoint = "Both tools work together: SharePoint for policy + MCP for implementation docs"
            }
        };

        Console.WriteLine("\n" + "".PadRight(70, '='));
        Console.WriteLine("🏢 MODERN WORKPLACE ASSISTANT - BUSINESS SCENARIO DEMONSTRATION");
        Console.WriteLine("".PadRight(70, '='));
        Console.WriteLine("This demonstration shows how AI agents solve real business problems");
        Console.WriteLine("using the Azure AI Projects v2 SDK with the Responses API.");
        Console.WriteLine("".PadRight(70, '='));

        for (int i = 0; i < scenarios.Length; i++)
        {
            var scenario = scenarios[i];
            Console.WriteLine($"\n📊 SCENARIO {i + 1}/{scenarios.Length}: {scenario.Title}");
            Console.WriteLine("".PadRight(50, '-'));
            Console.WriteLine($"❓ QUESTION: {scenario.Question}");
            Console.WriteLine($"🎯 BUSINESS CONTEXT: {scenario.Context}");
            Console.WriteLine($"🎓 LEARNING POINT: {scenario.LearningPoint}");
            Console.WriteLine("".PadRight(50, '-'));

            // <agent_conversation>
            Console.WriteLine("🤖 ASSISTANT RESPONSE:");
            var (response, status) = await ChatWithAssistantAsync(scenario.Question);
            // </agent_conversation>

            // Display response with analysis
            if (status == "completed" && !string.IsNullOrWhiteSpace(response) && response.Length > 10)
            {
                var preview = response.Length > 500 ? response.Substring(0, 500) + "..." : response;
                Console.WriteLine($"✅ SUCCESS: {preview}");
                if (response.Length > 500)
                {
                    Console.WriteLine($"   📏 Full response: {response.Length} characters");
                }
            }
            else
            {
                Console.WriteLine($"⚠️  RESPONSE: {response}");
            }

            Console.WriteLine($"📈 STATUS: {status}");
            Console.WriteLine("".PadRight(50, '-'));

            // Small delay between scenarios
            await Task.Delay(1000);
        }

        Console.WriteLine("\n✅ DEMONSTRATION COMPLETED!");
        Console.WriteLine("🎓 Key Learning Outcomes:");
        Console.WriteLine("   • Azure AI Projects v2 SDK with PromptAgentDefinition");
        Console.WriteLine("   • Responses API for agent conversations");
        Console.WriteLine("   • SharePoint + MCP tool integration");
        Console.WriteLine("   • MCP tool approval handling via the Responses API");
        Console.WriteLine("   • Real business value through AI assistance");
        Console.WriteLine("   • Foundation for governance and monitoring (Tutorials 2-3)");
    }

    /// <summary>
    /// Execute a conversation with the workplace assistant using the Responses API.
    /// 
    /// This function demonstrates the v2 conversation pattern including:
    /// - Sending a request via ProjectResponsesClient
    /// - MCP tool approval handling through the Responses API approval loop
    /// - Proper error and timeout management
    /// 
    /// Educational Value:
    /// - Shows the Responses API conversation pattern (replaces threads/runs)
    /// - Demonstrates MCP approval via McpToolCallApprovalRequestItem
    /// - Includes timeout and error management patterns
    /// </summary>
    // <mcp_approval_handler>
    private static async Task<(string response, string status)> ChatWithAssistantAsync(string message)
    {
        try
        {
            // Send the user message via the Responses API
            ResponseResult response = await responseClient!.CreateResponseAsync(message);

            // <mcp_approval_usage>
            // Handle MCP tool approval loop.
            // When the agent uses MCP tools, the response may contain
            // McpToolCallApprovalRequestItem items. We auto-approve and re-send.
            int maxIterations = 30;
            int iteration = 0;

            while (iteration < maxIterations)
            {
                // Check for MCP approval requests in the output items
                var approvalRequests = response.OutputItems
                    .OfType<McpToolCallApprovalRequestItem>()
                    .ToList();

                if (approvalRequests.Count == 0) break;

                // Build approval response items
                var approvalItems = new List<ResponseItem>();
                foreach (var request in approvalRequests)
                {
                    Console.WriteLine($"   🔧 Approving MCP tool: {request.ToolName}");

                    // Auto-approve MCP tool calls
                    // In production, you might implement custom approval logic here:
                    // - RBAC checks (is user authorized for this tool?)
                    // - Cost controls (has budget limit been reached?)
                    // - Logging and auditing
                    // - Interactive approval prompts
                    approvalItems.Add(ResponseItem.CreateMcpApprovalResponseItem(
                        request.Id,
                        approved: true));
                }

                // Send approval responses, chained to the previous response
                response = await responseClient.CreateResponseAsync(
                    approvalItems,
                    previousResponseId: response.Id);
                iteration++;
            }
            // </mcp_approval_usage>

            // Extract the text output
            string? outputText = response.GetOutputText();

            if (!string.IsNullOrWhiteSpace(outputText) && outputText.Length > 0)
            {
                return (outputText, "completed");
            }
            else
            {
                return ("No response from assistant", "completed");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Exception details: {ex.GetType().Name}: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"   Inner: {ex.InnerException.Message}");
            }
            return ($"Error in conversation: {ex.Message}", "failed");
        }
    }
    // </mcp_approval_handler>

    /// <summary>
    /// Interactive mode for testing the workplace assistant.
    /// 
    /// This provides a simple interface for users to test the agent with their own questions
    /// and see how it provides comprehensive technical guidance.
    /// Uses PreviousResponseId to maintain conversation context across turns.
    /// </summary>
    private static async Task InteractiveModeAsync(AgentVersion agentVersion)
    {
        Console.WriteLine("\n" + "".PadRight(60, '='));
        Console.WriteLine("💬 INTERACTIVE MODE - Test Your Workplace Assistant!");
        Console.WriteLine("".PadRight(60, '='));
        Console.WriteLine("Ask questions about Azure, M365, security, and technical implementation:");
        Console.WriteLine("• 'How do I configure Azure AD conditional access?'");
        Console.WriteLine("• 'What are MFA best practices for remote workers?'");
        Console.WriteLine("• 'How do I set up secure SharePoint access?'");
        Console.WriteLine("Type 'quit' to exit.");
        Console.WriteLine("".PadRight(60, '-'));

        while (true)
        {
            try
            {
                Console.Write("\n❓ Your question: ");
                string? question = Console.ReadLine()?.Trim();

                if (string.IsNullOrEmpty(question))
                {
                    Console.WriteLine("💡 Please ask a question about Azure or M365 technical implementation.");
                    continue;
                }

                if (question.ToLower() is "quit" or "exit" or "bye")
                {
                    break;
                }

                Console.Write("\n🤖 Workplace Assistant: ");
                var (response, status) = await ChatWithAssistantAsync(question);
                Console.WriteLine(response);

                if (status != "completed")
                {
                    Console.WriteLine($"\n⚠️  Response status: {status}");
                }

                Console.WriteLine("".PadRight(60, '-'));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ Error: {ex.Message}");
                Console.WriteLine("".PadRight(60, '-'));
            }
        }

        Console.WriteLine("\n👋 Thank you for testing the Modern Workplace Assistant!");
    }
}
