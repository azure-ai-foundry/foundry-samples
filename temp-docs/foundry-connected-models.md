# Use connected Foundry models in Agent Service

Foundry Agent Service lets you connect to models hosted in another Foundry resource and use them in your agents. This is part of the *bring your own model* capability, which lets you call models that live outside your current project without copying or redeploying them. For the full *bring your own model* experience — including third-party models — see [Bring your own model with the AI Gateway](https://learn.microsoft.com/azure/foundry/agents/how-to/ai-gateway).

This article focuses on connecting to **Foundry models** in another Foundry resource.

> [!NOTE]
> Classic agents used capability hosts to reach models in another resource. The new Agent Service doesn't use capability hosts for this. To use a model from another Foundry resource, create a model connection as described in this article.

## Choose a connection type

There are two ways to create a connection to models in another Foundry resource:

- **Azure API Management** — Connect to a Foundry resource that sits behind an Azure API Management (APIM) instance.
- **Other source** — Connect directly to another Foundry resource using its project endpoint.

Use **Azure API Management** when you already have an APIM in front of your Foundry resource, or when you want APIM to provide capabilities beyond connectivity, such as:

- Load balancing across backends
- Throttling and rate limiting
- Governance and guardrails

Use **Other source** when you want a quick, direct connection to another Foundry resource and don't need the additional controls that APIM provides.

## Configure a connection in the portal

### Azure API Management

For step-by-step instructions, see [Create a model connection](https://learn.microsoft.com/azure/foundry/agents/how-to/ai-gateway?tabs=api-management&pivots=foundry-portal#create-a-model-connection). Use the following values:

- **Connection details**
  - **Connection Type**: Azure API Management
  - **Azure API Management**: The name of your existing APIM resource
  - **Model API**: The name of the target Foundry resource
  - **Base URL**: `https://<apim-resource-name>.azure-api.net/<foundry-resource-name>`
- **Authentication**: Choose **API Key** or **Managed Identity**.
  - If you choose **API Key**:
    - **API Key Header Name**: `Authorization`
    - **API Key Header Value**: `Bearer {api_key}`
- **Model**: Provide the model details.
- **Advanced**: No additional configuration needed.

### Other source

For step-by-step instructions, see [Create a model connection](https://learn.microsoft.com/azure/foundry/agents/how-to/ai-gateway?tabs=other-sources&pivots=foundry-portal#create-a-model-connection). Use the following values:

- **Connection details**
  - **Connection Type**: Other source
  - **Connection Name**: A name of your choice
  - **Base URL**: `{project-endpoint}/openai/v1`

    You can find the project endpoint on the **Overview** page of the Foundry resource you want to connect to.
- **Authentication**: Choose **API Key** or **Managed Identity**.
  - If you choose **API Key**:
    - **API Key Header Name**: `Authorization`
    - **API Key Header Value**: `Bearer {api_key}`
- **Model**: Provide the model details.
- **Advanced**: No additional configuration needed.

## Use a connected model in an agent

Once the connection is created, the connected models appear in the model picker in the agents playground, and you can select them when authoring a prompt agent.

When you create agents in code, reference the connected model using the format `<connection-name>/<model-name>`. For an end-to-end example, see [Create a prompt agent with the model connection](https://learn.microsoft.com/azure/foundry/agents/how-to/ai-gateway?tabs=other-sources&pivots=foundry-portal#create-a-prompt-agent-with-the-model-connection).

## Configure connections from the CLI

You can also create and manage connections from the Azure CLI, which is useful for automation and CI/CD scenarios.

> [!NOTE]
> CLI samples and required configuration files for connected Foundry models are coming soon.

## Things to keep in mind

- A connection to another Foundry resource does not automatically grant access to every model in that resource. You must explicitly add the models you want to expose, which gives you precise control over what's available to your agents.
- For the **Other source** connection type, the portal creates the connection at the **resource** scope, which makes it available to every project in the resource. To restrict a connection to specific projects, create it from the CLI at the project scope.

  > [!NOTE]
  > Detailed CLI instructions for project-scoped connections are coming soon.