using Azure.AI.Agents.Persistent;
using Azure.Identity;
using System;
using Microsoft.Extensions.Configuration;
using System.Threading;
using Azure;

// Get Connection information from App Configuration
IConfigurationRoot configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

var projectEndpoint = configuration["ProjectEndpoint"];
var modelDeploymentName = configuration["ModelDeploymentName"];
var fabricConnectionId = configuration["FabricConnectionId"];

// Create the Agent Client
PersistentAgentsClient agentClient = new(projectEndpoint, new DefaultAzureCredential());

// Create the MicrosoftFabricToolDefinition object needed when creating the agent
ToolConnectionList connectionList = new()
{
    ConnectionList = { new ToolConnection(fabricConnectionId) }
};
MicrosoftFabricToolDefinition fabricTool = new(connectionList);

// Create the Agent
PersistentAgent agent = agentClient.Administration.CreateAgent(
    model: modelDeploymentName,
    name: "my-assistant",
    instructions: "You are a helpful assistant.",
    tools: [fabricTool]);

PersistentAgentThread thread = agentClient.Threads.CreateThread();

// Create message and run the agent
ThreadMessage message = agentClient.Messages.CreateMessage(
    thread.Id,
    MessageRole.User,
    "What are the top 3 weather events with highest property damage?");
ThreadRun run = agentClient.Runs.CreateRun(thread, agent);

// Wait for the agent to finish running
do
{
    Thread.Sleep(TimeSpan.FromMilliseconds(500));
    run = agentClient.Runs.GetRun(thread.Id, run.Id);
}
while (run.Status == RunStatus.Queued
    || run.Status == RunStatus.InProgress);

// Confirm that the run completed successfully
if (run.Status != RunStatus.Completed)
{
    throw new Exception("Run did not complete successfully, error: " + run.LastError?.Message);
}

// Retrieve all messages from the agent client
Pageable<ThreadMessage> messages = agentClient.Messages.GetMessages(
    threadId: thread.Id,
    order: ListSortOrder.Ascending
);

// Process messages in order
foreach (ThreadMessage threadMessage in messages)
{
    Console.Write($"{threadMessage.CreatedAt:yyyy-MM-dd HH:mm:ss} - {threadMessage.Role,10}: ");
    foreach (MessageContent contentItem in threadMessage.ContentItems)
    {
        if (contentItem is MessageTextContent textItem)
        {
            string response = textItem.Text;

            // If we have Text URL citation annotations, reformat the response to show title & URL for citations
            if (textItem.Annotations != null)
            {
                foreach (MessageTextAnnotation annotation in textItem.Annotations)
                {
                    if (annotation is MessageTextUrlCitationAnnotation urlAnnotation)
                    {
                        response = response.Replace(urlAnnotation.Text, $" [{urlAnnotation.UrlCitation.Title}]({urlAnnotation.UrlCitation.Url})");
                    }
                }
            }
            Console.Write($"Agent response: {response}");
        }
        else if (contentItem is MessageImageFileContent imageFileItem)
        {
            Console.Write($"<image from ID: {imageFileItem.FileId}");
        }
        Console.WriteLine();
    }
}

// Delete thread and agent
agentClient.Threads.DeleteThread(threadId: thread.Id);
agentClient.Administration.DeleteAgent(agentId: agent.Id);
