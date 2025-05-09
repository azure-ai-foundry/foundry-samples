import "dotenv/config";
import {
  AIProjectsClient,
  DoneEvent,
  ErrorEvent,
  isOutputOfType,
  MessageStreamEvent,
  RunStreamEvent,
  ToolUtility,
  type MessageDeltaChunk,
  type MessageDeltaTextContent,
  type MessageTextContentOutput,
} from "@azure/ai-projects";
import { DefaultAzureCredential } from "@azure/identity";

const connectionString = process.env.PROJECT_CONNECTION_STRING;
const model = process.env.MODEL_DEPLOYMENT_NAME ?? "gpt-4o";
const connectionName = process.env.LEGALFLY_API_CONNECTION_NAME;

if (!connectionString) {
  throw new Error(
    "Please set the PROJECT_CONNECTION_STRING environment variable."
  );
}

const client = AIProjectsClient.fromConnectionString(
  connectionString,
  new DefaultAzureCredential()
);

async function fetchOpenApiSpec() {
  const openApiSpecUrl = "https://public-api.legalfly.com/docs/json";
  const response = await fetch(openApiSpecUrl);
  if (!response.ok) {
    throw new Error(`Failed to fetch OpenAPI spec: ${response.statusText}`);
  }
  return response.json();
}

async function main() {
  if (!connectionName) {
    throw new Error(
      "Please set the LEGALFLY_API_CONNECTION_NAME environment variable."
    );
  }

  const connection = await client.connections.getConnectionWithSecrets(
    connectionName
  );

  console.log(`Created connection, connection ID: ${connection.id}`);

  const openApiSpec = await fetchOpenApiSpec();
  const openApiTool = ToolUtility.createOpenApiTool({
    spec: openApiSpec,
    name: "legalCounsel",
    auth: {
      type: "connection",
      // @ts-ignore
      security_scheme: {
        connectionId: connection.id,
      },
    },
  });

  const agent = await client.agents.createAgent(model, {
    name: "my-agent",
    instructions:
      "You are a helpful AI legal assistant. Act like a friendly person who possesses a lot of legal knowledge.",
    tools: [openApiTool.definition],
  });
  console.log(`Created agent, agent ID: ${agent.id}`);

  const thread = await client.agents.createThread();
  console.log(`Created thread, thread ID: ${thread.id}`);

  const message = await client.agents.createMessage(thread.id, {
    role: "user",
    content: "What do I need to start a company in California?",
  });
  console.log(`Created message, message ID: ${message.id}`);

  const streamEventMessages = await client.agents
    .createRun(thread.id, agent.id)
    .stream();

  for await (const eventMessage of streamEventMessages) {
    switch (eventMessage.event) {
      case RunStreamEvent.ThreadRunCreated:
        console.log(`Run created: ${eventMessage.data}`);
        break;
      case MessageStreamEvent.ThreadMessageDelta:
        {
          const messageDelta = eventMessage.data as MessageDeltaChunk;
          messageDelta.delta.content.forEach((contentPart) => {
            if (contentPart.type === "text") {
              const textContent = contentPart as MessageDeltaTextContent;
              const textValue = textContent.text?.value || "No text";
              console.log(`Text: ${textValue}`);
            }
          });
        }
        break;

      case RunStreamEvent.ThreadRunCompleted:
        console.log(`Run completed: ${eventMessage.data}`);
        break;
      case ErrorEvent.Error:
        console.log(`An error occurred. Data ${eventMessage.data}`);
        break;
      case DoneEvent.Done:
        console.log(`Done: ${eventMessage.data}`);
        break;
    }
  }

  // 6. Print the messages from the agent
  const messages = await client.agents.listMessages(thread.id);

  // Messages iterate from oldest to newest
  // messages[0] is the most recent
  const messagesArray = messages.data;
  for (let i = messagesArray.length - 1; i >= 0; i--) {
    const m = messagesArray[i];
    console.log(`Type: ${m.content[0].type}`);
    if (isOutputOfType<MessageTextContentOutput>(m.content[0], "text")) {
      const textContent = m.content[0] as MessageTextContentOutput;
      console.log(`Text: ${textContent.text.value}`);
    }
  }

  // 7. Delete the agent once done
  await client.agents.deleteAgent(agent.id);
}

main().catch((err) => {
  console.error("The sample encountered an error:", err);
  process.exit(1);
});
