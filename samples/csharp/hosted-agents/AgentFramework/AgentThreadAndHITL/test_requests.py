import json
import os
import secrets
import string

import requests


base_url = os.getenv("AGENTSERVER_URL", "http://localhost:8088").rstrip("/")
url = base_url if base_url.endswith("/responses") else f"{base_url}/responses"
stream = False


alphanum = string.ascii_letters + string.digits


def create_conversation_id():
    # Match AgentServer expected format: conv_<18-char partition><32-char entropy>
    return "conv_" + "".join(secrets.choice(alphanum) for _ in range(50))


def extract_conversation_id(response_detail):
    conversation = response_detail.get("conversation")
    if isinstance(conversation, dict):
        conversation_id = conversation.get("id")
        if isinstance(conversation_id, str) and conversation_id:
            return conversation_id
    return None


user_input = "What is the weather like in Vancouver?"
conversation_id = create_conversation_id()
payload = {
    "agent": {"name": "local_agent", "type": "agent_reference"},
    "tools": [],
    "stream": stream,
    "input": user_input,
    "conversation": {"id": conversation_id},
}

call_id = None

try:
    response = requests.post(url, json=payload)
    response.raise_for_status()

    response_detail = response.json()
    print(json.dumps(response_detail, indent=2))

    returned_conversation_id = extract_conversation_id(response_detail)
    if returned_conversation_id:
        conversation_id = returned_conversation_id

    output = response_detail.get("output", [])
    if isinstance(output, list):
        for item in output:
            if item.get("type") == "function_call" and item.get("name") == "__hosted_agent_adapter_hitl__":
                call_id = item.get("call_id")
                break
except Exception as e:
    print(f"Error: {e}")

print("\n\n")
print(f"conversation_id: {conversation_id}")
print(f"call_id: {call_id}")

if not call_id:
    print("Failed to parse hitl request info")
else:
    human_feedback = {
        "call_id": call_id,
        "output": "approve",
        "type": "function_call_output",
    }

    feedback_payload = {
        "agent": {"name": "local_agent", "type": "agent_reference"},
        "tools": [],
        "stream": stream,
        "input": [human_feedback],
        "conversation": {"id": conversation_id},
    }

    try:
        print("\n\nsending feedback...")
        print(json.dumps(feedback_payload, indent=2))
        response = requests.post(url, json=feedback_payload)
        response.raise_for_status()
        print("\n\nagent response:")
        print(json.dumps(response.json(), indent=2))
    except Exception as e:
        print(f"Error: {e}")
