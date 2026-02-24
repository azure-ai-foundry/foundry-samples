# Generate a response using the agent

curl -X POST https://YOUR-FOUNDRY-RESOURCE-NAME.services.ai.azure.com/api/projects/YOUR-PROJECT-NAME/openai/v1/responses \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $AZURE_AI_AUTH_TOKEN" \
  -d '{
    "agent_reference": {"type": "agent_reference", "name": "<AGENT_NAME>"},
    "input": [{"role": "user", "content": "What is the size of France in square miles?"}]
  }'

# Optional Step: Create a conversation to use with the agent
curl -X POST https://YOUR-FOUNDRY-RESOURCE-NAME.services.ai.azure.com/api/projects/YOUR-PROJECT-NAME/openai/v1/conversations \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $AZURE_AI_AUTH_TOKEN" \
  -d '{
    "items": [
      {
        "type": "message",
        "role": "user",
        "content": [
          {
            "type": "input_text",
            "text": "What is the size of France in square miles?"
          }
        ]
      }
    ]
  }'

# Lets say Conversation ID created is conv_123456789. Use this in the next step

#Optional Step: Ask a follow-up question in the same conversation
curl -X POST https://YOUR-FOUNDRY-RESOURCE-NAME.services.ai.azure.com/api/projects/YOUR-PROJECT-NAME/openai/v1/responses \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $AZURE_AI_AUTH_TOKEN" \
  -d '{
    "agent_reference": {"type": "agent_reference", "name": "<AGENT_NAME>", "version": "1"},
    "conversation": "<CONVERSATION_ID>",
    "input": [{"role": "user", "content": "And what is the capital?"}]
  }'