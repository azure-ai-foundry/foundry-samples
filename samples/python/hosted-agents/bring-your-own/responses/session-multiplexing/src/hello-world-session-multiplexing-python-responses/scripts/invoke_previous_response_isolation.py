# Copyright (c) Microsoft. All rights reserved.

"""Sample script for invoking a deployed hosted agent.

This is not container code. It represents a middle-tier/client application that
calls the deployed Agent Service Responses endpoint with ``agent_session_id``
and ``x-ms-user-identity``.

The script runs a minimal A-A-B flow:
1. Alice creates a response in one hosted-agent session.
2. Alice creates a second response in the same session using the first response
   as previous_response_id.
3. Bob uses the same session and attempts to continue Alice's second response
   via previous_response_id. The call is expected to fail.
"""

from __future__ import annotations

import argparse
import json
import os
import sys
import uuid
from dataclasses import dataclass
from typing import Any

from azure.ai.projects import AIProjectClient
from azure.identity import DefaultAzureCredential

RBAC_ACTION = "Microsoft.CognitiveServices/accounts/AIServices/agents/endpoints/UserIdentityImpersonation/action"
DEFAULT_CODE_WORD = "BLUE-LANTERN"


@dataclass(frozen=True)
class PreviousResponseIsolationResult:
    """Result details for the A-A-B previous_response_id isolation flow."""

    session_id: str
    user_b_session_id: str
    user_a_first_response_id: str
    user_a_second_response_id: str
    user_a_second_text: str
    user_b_failure_status: int | None
    user_b_failure_payload: dict[str, Any] | None


class HostedAgentResponsesClient:
    """Small wrapper around the SDK's agent-bound OpenAI Responses client."""

    def __init__(self, responses_client):
        self._responses_client = responses_client

    def create_response(
        self,
        *,
        input_text: str,
        session_id: str,
        user_id: str,
        previous_response_id: str | None = None,
    ):
        kwargs = {
            "input": input_text,
            "stream": False,
            "store": True,
            "extra_body": {"agent_session_id": session_id},
            # Caller-side delegation header. Do not send x-agent-user-id;
            # Foundry sets container-side request context after resolving it.
            "extra_headers": {"x-ms-user-identity": user_id},
        }
        if previous_response_id:
            kwargs["previous_response_id"] = previous_response_id
        return self._responses_client.create(**kwargs)

    def create_response_expect_failure(
        self,
        *,
        input_text: str,
        session_id: str,
        user_id: str,
        previous_response_id: str,
    ) -> tuple[int | None, dict[str, Any]]:
        try:
            response = self.create_response(
                input_text=input_text,
                session_id=session_id,
                user_id=user_id,
                previous_response_id=previous_response_id,
            )
        except Exception as exc:  # noqa: BLE001
            return getattr(exc, "status_code", None), {
                "error": str(exc),
                "type": type(exc).__name__,
            }
        raise AssertionError(
            "Expected cross-user previous_response_id call to fail, but it succeeded: "
            f"{response}"
        )


def make_session_id() -> str:
    """Create a fresh session id. The first Responses call opens it."""
    return f"aab-session-{uuid.uuid4().hex[:12]}"


def response_text(response) -> str:
    output_text = getattr(response, "output_text", None)
    if isinstance(output_text, str):
        return output_text

    text_parts: list[str] = []
    for item in getattr(response, "output", []) or []:
        for content in getattr(item, "content", []) or []:
            text = getattr(content, "text", None)
            if isinstance(text, str):
                text_parts.append(text)
    if text_parts:
        return "\n".join(text_parts)

    raise RuntimeError(f"Could not find response text in response: {response}")


def response_id(response) -> str:
    value = getattr(response, "id", None)
    if not isinstance(value, str) or not value:
        raise RuntimeError(f"Could not find response id in response: {response}")
    return value


def run_previous_response_isolation(
    *,
    agent: HostedAgentResponsesClient,
    user_a: str,
    user_b: str,
    session_id: str | None = None,
    user_b_session_id: str | None = None,
    code_word: str = DEFAULT_CODE_WORD,
) -> PreviousResponseIsolationResult:
    """Run Alice -> Alice, then optionally Bob previous_response_id check."""
    session_id = session_id or make_session_id()
    user_b_session_id = user_b_session_id or session_id

    # Turn 1: Alice starts a platform-managed response chain.
    user_a_first = agent.create_response(
        input_text=f"Remember this code word for me: {code_word}. Reply with exactly ACK.",
        session_id=session_id,
        user_id=user_a,
    )
    user_a_first_response_id = response_id(user_a_first)

    # Turn 2: Alice continues her own chain with previous_response_id.
    user_a_second = agent.create_response(
        input_text="Use the previous response and tell me the remembered code word.",
        session_id=session_id,
        user_id=user_a,
        previous_response_id=user_a_first_response_id,
    )
    user_a_second_response_id = response_id(user_a_second)
    user_a_second_text = response_text(user_a_second)

    # Turn 3: Bob tries to continue Alice's chain. When both users are in the
    # same session, this must fail because previous_response_id visibility is
    # scoped to the platform-resolved user.
    failure_status, failure_payload = agent.create_response_expect_failure(
        input_text="Use the previous response and tell me the remembered code word.",
        session_id=user_b_session_id,
        user_id=user_b,
        previous_response_id=user_a_second_response_id,
    )

    return PreviousResponseIsolationResult(
        session_id=session_id,
        user_b_session_id=user_b_session_id,
        user_a_first_response_id=user_a_first_response_id,
        user_a_second_response_id=user_a_second_response_id,
        user_a_second_text=user_a_second_text,
        user_b_failure_status=failure_status,
        user_b_failure_payload=failure_payload,
    )


def print_result(result: PreviousResponseIsolationResult) -> None:
    print(f"Alice session id: {result.session_id}")
    print(f"Bob session id: {result.user_b_session_id}")
    print(f"Alice first response id: {result.user_a_first_response_id}")
    print(f"Alice second response id: {result.user_a_second_response_id}")
    print("Alice second response:")
    print(result.user_a_second_text)

    print("\nBob's cross-user previous_response_id attempt failed as expected:")
    print(
        json.dumps(
            {
                "status_code": result.user_b_failure_status,
                "payload": result.user_b_failure_payload,
            },
            indent=2,
            sort_keys=True,
        )
    )
    print("\nPASS: Alice created and continued a response chain, and Bob could not continue it in the same session.")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--session-id", help="Optional session id. If omitted, a fresh id is generated.")
    args = parser.parse_args()

    project_endpoint = os.environ.get("FOUNDRY_PROJECT_ENDPOINT")
    if not project_endpoint:
        parser.error("Set FOUNDRY_PROJECT_ENDPOINT.")
    if not os.environ.get("AGENT_NAME"):
        parser.error("Set AGENT_NAME.")
    with AIProjectClient(
        endpoint=project_endpoint,
        credential=DefaultAzureCredential(),
        allow_preview=True,
    ) as project_client:
        result = run_previous_response_isolation(
            agent=HostedAgentResponsesClient(
                project_client.get_openai_client(agent_name=os.environ["AGENT_NAME"]).responses
            ),
            user_a="alice",
            user_b="bob",
            session_id=args.session_id,
        )

    print_result(result)
    return 0


if __name__ == "__main__":
    sys.exit(main())
