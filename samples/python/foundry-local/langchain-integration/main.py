"""Translation application using LangChain with Foundry Local."""

# <langchain_translation>
import os
from langchain_openai import ChatOpenAI
from langchain_core.prompts import ChatPromptTemplate
from foundry_local import FoundryLocalManager

# By using an alias, the most suitable model will be downloaded
# to your end-user's device.
# TIP: You can find a list of available models by running the
# following command: `foundry model list`.
alias = "qwen2.5-0.5b"

# Create a FoundryLocalManager instance. This will start the Foundry
# Local service if it is not already running and load the specified model.
manager = FoundryLocalManager(alias)

# Configure ChatOpenAI to use your locally-running model
llm = ChatOpenAI(
    model=manager.get_model_info(alias).id,
    base_url=manager.endpoint,
    api_key=manager.api_key,
    temperature=0.6,
    streaming=False
)

# Create a translation prompt template
prompt = ChatPromptTemplate.from_messages([
    (
        "system",
        "You are a helpful assistant that translates {input_language} to {output_language}."
    ),
    ("human", "{input}")
])

# Build a simple chain by connecting the prompt to the language model
chain = prompt | llm

input = "I love to code."
print(f"Translating '{input}' to French...")

# Run the chain with your inputs
ai_msg = chain.invoke({
    "input_language": "English",
    "output_language": "French",
    "input": input
})

# print the result content
print(f"Response: {ai_msg.content}")
# </langchain_translation>
