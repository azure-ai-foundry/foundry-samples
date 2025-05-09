# LEGALFLY

## Description

Legal insights grounded in trusted sources from your jurisdiction.

## Prerequisites

- Obtain an API key for your [LEGALFLY developer account](https://www.legalfly.com/ai-foundry-agents).

## Setup

1. Go to [Azure AI Foundry portal](https://ai.azure.com/) and select your AI Project. Select **Management Center**.
2. Select **+new connection** in the settings page.
3. Select **custom keys** in **other resource types**.
4. Enter the following information to create a connection to store your LEGALFLY key:
   - Set **Custom keys** to "key", with the value being your LEGALFLY API key.
   - Make sure **is secret** is checked.
   - Set the connection name to your connection name. You use this connection name in your sample code or Foundry Portal later.
   - For the **Access** setting, you can choose either _this project only_ or _shared to all projects_. Just make sure in your code, the connection string of the project you entered has access to this connection.

## Use LEGALFLY

1. To use the LEGALFLY tool in Azure AI Foundry, in the **Create and debug** screen for your agent, scroll down the **Setup** pane on the right to **action**. Then select **Add**.
2. Select **LEGALFLY** and follow the prompts to add the tool.
3. Give a name for your LEGALFLY tool and provide an optional description.
4. Select the custom key connection you just created.
5. Finish and start chatting.

## Customer Support Contact
