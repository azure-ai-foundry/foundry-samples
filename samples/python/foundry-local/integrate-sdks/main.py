"""Foundry Local integration examples using OpenAI SDK, streaming, and requests."""
import openai
import requests
import json
from foundry_local import FoundryLocalManager


# <basic_chat_completion>
def basic_chat_completion():
    """Basic chat completion using OpenAI SDK with Foundry Local."""
    # By using an alias, the most suitable model will be downloaded
    # to your end-user's device.
    alias = "qwen2.5-0.5b"

    # Create a FoundryLocalManager instance. This will start the Foundry
    # Local service if it is not already running and load the specified model.
    manager = FoundryLocalManager(alias)
    # The remaining code uses the OpenAI Python SDK to interact with the local model.
    # Configure the client to use the local Foundry service
    client = openai.OpenAI(
        base_url=manager.endpoint,
        api_key=manager.api_key  # API key is not required for local usage
    )
    # Set the model to use and generate a response
    response = client.chat.completions.create(
        model=manager.get_model_info(alias).id,
        messages=[{"role": "user", "content": "What is the golden ratio?"}]
    )
    print(response.choices[0].message.content)
# </basic_chat_completion>


# <streaming_chat_completion>
def streaming_chat_completion():
    """Streaming chat completion using OpenAI SDK with Foundry Local."""
    # By using an alias, the most suitable model will be downloaded
    # to your end-user's device.
    alias = "qwen2.5-0.5b"

    # Create a FoundryLocalManager instance. This will start the Foundry
    # Local service if it is not already running and load the specified model.
    manager = FoundryLocalManager(alias)

    # The remaining code uses the OpenAI Python SDK to interact with the local model.

    # Configure the client to use the local Foundry service
    client = openai.OpenAI(
        base_url=manager.endpoint,
        api_key=manager.api_key  # API key is not required for local usage
    )

    # Set the model to use and generate a streaming response
    stream = client.chat.completions.create(
        model=manager.get_model_info(alias).id,
        messages=[{"role": "user", "content": "What is the golden ratio?"}],
        stream=True
    )

    # Print the streaming response
    for chunk in stream:
        if chunk.choices[0].delta.content is not None:
            print(chunk.choices[0].delta.content, end="", flush=True)
# </streaming_chat_completion>


# <requests_chat_completion>
def requests_chat_completion():
    """Chat completion using the requests library with Foundry Local."""
    # By using an alias, the most suitable model will be downloaded
    # to your end-user's device.
    alias = "qwen2.5-0.5b"

    # Create a FoundryLocalManager instance. This will start the Foundry
    # Local service if it is not already running and load the specified model.
    manager = FoundryLocalManager(alias)

    url = manager.endpoint + "/chat/completions"

    payload = {
        "model": manager.get_model_info(alias).id,
        "messages": [
            {"role": "user", "content": "What is the golden ratio?"}
        ]
    }

    headers = {
        "Content-Type": "application/json"
    }

    response = requests.post(url, headers=headers, data=json.dumps(payload))
    print(response.json()["choices"][0]["message"]["content"])
# </requests_chat_completion>


if __name__ == "__main__":
    print("=== Basic Chat Completion ===")
    basic_chat_completion()
    print("\n\n=== Streaming Chat Completion ===")
    streaming_chat_completion()
    print("\n\n=== Requests Chat Completion ===")
    requests_chat_completion()
