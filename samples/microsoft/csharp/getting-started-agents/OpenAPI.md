# How to use the OpenAPI spec tool

In this example we will demonstrate the possibility to use services with [OpenAPI Specification](https://en.wikipedia.org/wiki/OpenAPI_Specification) with the agent. We will use [wttr.in](https://wttr.in) service to get weather and its specification file [weather_openapi.json](https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/ai/Azure.AI.Projects/tests/Samples/Agent/weather_openapi.json).

1. First get `ProjectEndpoint` and `ModelDeploymentName` from config and create a `PersistentAgentsClient`. Also, create an `OpenApiAnonymousAuthDetails` and `OpenApiToolDefinition` from config. 

```C# Snippet:AgentsOpenAPICallingExample_CreateClient
var projectEndpoint = configuration["ProjectEndpoint"];
var modelDeploymentName = configuration["ModelDeploymentName"];
var openApiSpec = configuration["OpenApiSpec"];
PersistentAgentsClient client = new(new Uri(projectEndpoint), new DefaultAzureCredential());

var spec = BinaryData.FromBytes(File.ReadAllBytes(openApiSpec));
OpenApiAnonymousAuthDetails openApiAnonAuth = new();
OpenApiToolDefinition openApiTool = new(
    name: "get_weather",
    description: "Retrieve weather information for a location",
    spec: spec,
    auth: openApiAnonAuth,
    defaultParams: ["format"]
);
```

2. Next we will need to create an agent.

Synchronous sample:

```C# Snippet:AgentsOverviewCreateAgentSync
PersistentAgent agent = client.CreateAgent(
    model: modelDeploymentName,
    name: "Open API Tool Calling Agent",
    instructions: "You are a helpful agent.",
    tools: [openApiTool]
);
```

Asynchronous sample:

```C# Snippet:AgentsOverviewCreateAgent
PersistentAgent agent = await client.CreateAgentAsync(
    model: modelDeploymentName,
    name: "Open API Tool Calling Agent",
    instructions: "You are a helpful agent.",
    tools: [openApiTool]
);
```

3. Now we will create a `ThreadRun` and wait until it is complete. If the run will not be successful, we will print the last error.

Synchronous sample:
```C# Snippet:AgentsOpenAPISyncHandlePollingWithRequiredAction
PersistentAgentThread thread = client.CreateThread();
ThreadMessage message = client.CreateMessage(
    thread.Id,
    MessageRole.User,
    "What's the weather in Seattle?");

ThreadRun run = client.CreateRun(thread, agent);

do
{
    Thread.Sleep(TimeSpan.FromMilliseconds(500));
    run = client.GetRun(thread.Id, run.Id);
}
while (run.Status == RunStatus.Queued
    || run.Status == RunStatus.InProgress
    || run.Status == RunStatus.RequiresAction);
```

Asynchronous sample:

```C# Snippet:AgentsOpenAPIHandlePollingWithRequiredAction
PersistentAgentThread thread = await client.CreateThreadAsync();
ThreadMessage message = await client.CreateMessageAsync(
    thread.Id,
    MessageRole.User,
    "What's the weather in Seattle?");

ThreadRun run = await client.CreateRunAsync(thread, agent);

do
{
    await Task.Delay(TimeSpan.FromMilliseconds(500));
    run = await client.GetRunAsync(thread.Id, run.Id);
}
while (run.Status == RunStatus.Queued
    || run.Status == RunStatus.InProgress
    || run.Status == RunStatus.RequiresAction);
```

4. Print the messages to the console in chronological order.

Synchronous sample:

```C# Snippet:AgentsOpenAPISync_Print
PageableList<ThreadMessage> messages = client.GetMessages(
    threadId: thread.Id,
    order: ListSortOrder.Ascending
);

foreach (ThreadMessage threadMessage in messages)
{
    foreach (MessageContent contentItem in threadMessage.ContentItems)
    {
        if (contentItem is MessageTextContent textItem)
        {
            Console.Write($"{threadMessage.Role}: {textItem.Text}");
        }
        Console.WriteLine();
    }
}
```

Asynchronous sample:

```C# Snippet:AgentsOpenAPI_Print
PageableList<ThreadMessage> messages = await client.GetMessagesAsync(
    threadId: thread.Id,
    order: ListSortOrder.Ascending
);

foreach (ThreadMessage threadMessage in messages)
{
    foreach (MessageContent contentItem in threadMessage.ContentItems)
    {
        if (contentItem is MessageTextContent textItem)
        {
            Console.Write($"{threadMessage.Role}: {textItem.Text}");
        }
        Console.WriteLine();
    }
}
```

5. Finally, we delete all the resources, we have created in this sample.

Synchronous sample:

```C# Snippet:AgentsOpenAPISync_Cleanup
client.DeleteThread(thread.Id);
client.DeleteAgent(agent.Id);
```

Asynchronous sample:

```C# Snippet:AgentsOpenAPI_Cleanup
await client.DeleteThreadAsync(thread.Id);
await client.DeleteAgentAsync(agent.Id);
```
