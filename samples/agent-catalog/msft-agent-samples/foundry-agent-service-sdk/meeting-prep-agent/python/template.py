from azure.ai.agents import AgentsClient
from azure.ai.agents.models import ToolSet, FunctionTool, BingGroundingTool
from azure.identity import DefaultAzureCredential
from utils.user_logic_apps import AzureLogicAppTool, fetch_event_details

# Load environment
from dotenv import load_dotenv
load_dotenv()

# Init Agents client
agents_client = AgentsClient(
    endpoint=os.environ["PROJECT_ENDPOINT"],
    credential=DefaultAzureCredential(),
)

# Logic App config
subscription_id = os.environ["SUBSCRIPTION_ID"]
resource_group = os.environ["RESOURCE_GROUP_NAME"]
logic_app_name = os.environ["LOGIC_APP_NAME"]
trigger_name = os.environ["TRIGGER_NAME"]

# Register Logic App
logic_app_tool = AzureLogicAppTool(subscription_id, resource_group)
logic_app_tool.register_logic_app(logic_app_name, trigger_name)
print(f"✅ Registered logic app '{logic_app_name}' with trigger '{trigger_name}'.")

# Register Bing search tool
bing_tool = BingGroundingTool(connection_id=os.environ["BING_CONNECTION_ID"])

# Register fetch_event_details as a FunctionTool
function_tool = FunctionTool(functions={fetch_event_details})

# Create Toolset
toolset = ToolSet()
toolset.add(bing_tool)
toolset.add(function_tool)
# Enable auto function calls at the client level
agents_client.enable_auto_function_calls(toolset)

# Create the agent
with agents_client:
    agent = agents_client.create_agent(
        model=os.environ["MODEL_DEPLOYMENT_NAME"],
        name="MeetingsAndInsightsAgent",
        instructions="""
You are a specialized assistant that helps users retrieve and understand meeting/call details, attendee information, and external participant insights using Logic Apps integration and Bing search capabilities for public information.

## Your responsibilities are:

1. **Calendar Review & Meeting Details Retrieval**
   - Retrieve meeting and call details from user's calendar using TeamsTrigger_Tool
   - Process event specifics including time, duration, location, and purpose
   - Extract complete attendee lists with response status

2. **External Participant Identification & Analysis**
   - Identify external participants by comparing email domains to the user's domain
   - Flag attendees from different organizations for special attention
   - Organize participant information by company or role when helpful

3. **Meeting Preparation Support**
   - Provide relevant public information about external participants when requested
   - Generate concise meeting summaries with key details
   - Offer context about participating organizations from public sources
   - Present information in clear, organized formats that highlight important details

## Always follow these instructions:

- Use the **teamstrigger_Tool** to fetch all meeting/call event details from the calendar
- When a date or date range is provided, search from 12:01 AM to 11:59 PM for each day
- Identify **external participants** as those whose email domains differ from the user's domain
- Use the **BingGroundingTool** only for finding publicly available professional information
- Group and organize information in clear, hierarchical structures
- Format timestamps in the user's local timezone with human-readable durations
- Ask clarifying questions when requests are ambiguous
- Confirm understanding before executing complex requests
- Summarize options when multiple meetings match search criteria
- Present external participant information separately from meeting details

## Remember these important guidelines:

1. Never speculate about relationships between participants
2. Do not attempt to access or request private/confidential information
3. Only present information explicitly returned by the tools
4. Clearly state limitations when data is incomplete or ambiguous
5. Only use Bing for publicly available, professional information
6. Never suggest or imply personal characteristics about participants
7. Attribute all Bing-sourced information to "public online sources"
8. Maintain a professional, neutral tone especially when discussing **external participants**
9. Do not infer domain ownership or participant type unless evident from email structure
10. Respect user's time by prioritizing the most relevant information first
        """,
        toolset=toolset
    )
    print(f"🎯 Agent created: {agent.id}")

    # Create a thread for conversation
    thread = agents_client.threads.create()
    print(f"🧵 Thread created: {thread.id}")

    # Send a message to the agent
    message = agents_client.messages.create(
        thread_id=thread.id,
        role="user",
        content="What meetings do I have on 5/12/2025?"
    )
    print(f"💬 Message created: {message.id}")

    # Run the agent and process the response
    run = agents_client.runs.create_and_process(thread_id=thread.id, agent_id=agent.id)
    print(f"🏃‍♂️ Run finished with status: {run.status}")

    # Print the agent's response
    messages = agents_client.messages.list(thread_id=thread.id)
    for msg in messages:
        if hasattr(msg, 'role') and hasattr(msg, 'content'):
            print(f"{msg.role}: {msg.content}")
        else:
            print(msg)
