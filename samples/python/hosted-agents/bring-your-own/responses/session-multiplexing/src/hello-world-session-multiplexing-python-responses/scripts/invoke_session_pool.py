# Copyright (c) Microsoft. All rights reserved.

"""Sample script for demonstrating caller-owned session-pool strategies.

This is not container code. It shows how a middle-tier/client application can
choose an ``agent_session_id`` before invoking the deployed hosted agent.
"""

from __future__ import annotations

import argparse
import os
import sys
from dataclasses import dataclass, field

from azure.ai.projects import AIProjectClient
from azure.identity import DefaultAzureCredential

from invoke_previous_response_isolation import (
    HostedAgentResponsesClient,
    print_result,
    run_previous_response_isolation,
)

ALICE_ID = "alice"
BOB_ID = "bob"


@dataclass
class SessionPool:
    """Caller-owned assignment with simple session selection strategies."""

    max_users_per_session: int
    strategy: str = "sticky-fill"
    pool_size: int = 1
    session_user_counts: dict[str, int] = field(default_factory=dict)
    user_to_session: dict[str, str] = field(default_factory=dict)
    next_round_robin_index: int = 0

    def __post_init__(self) -> None:
        if self.max_users_per_session < 1:
            raise ValueError("max_users_per_session must be at least 1.")
        if self.pool_size < 1:
            raise ValueError("pool_size must be at least 1.")
        if self.strategy not in {"sticky-fill", "round-robin"}:
            raise ValueError(f"Unsupported strategy: {self.strategy}")
        if self.strategy == "round-robin":
            for index in range(self.pool_size):
                self.session_user_counts.setdefault(self._session_name(index), 0)

    def _session_name(self, index: int) -> str:
        return f"shared-session-{index:03d}"

    def get_session_for_user(self, user_id: str) -> str:
        """Return an existing sticky assignment or assign a new session.

        Sticky means known users keep their mapped session. New users are
        assigned according to the selected strategy.
        """
        if user_id in self.user_to_session:
            return self.user_to_session[user_id]

        if self.strategy == "round-robin":
            session_id = self._next_round_robin_session()
        else:
            session_id = self._next_fill_session()

        self.user_to_session[user_id] = session_id
        self.session_user_counts[session_id] += 1
        return session_id

    def _next_fill_session(self) -> str:
        session_id = next(
            (
                candidate
                for candidate, count in self.session_user_counts.items()
                if count < self.max_users_per_session
            ),
            None,
        )
        if session_id is None:
            session_id = self._session_name(len(self.session_user_counts))
            self.session_user_counts[session_id] = 0
        return session_id

    def _next_round_robin_session(self) -> str:
        sessions = list(self.session_user_counts)
        for offset in range(len(sessions)):
            index = (self.next_round_robin_index + offset) % len(sessions)
            session_id = sessions[index]
            if self.session_user_counts[session_id] < self.max_users_per_session:
                self.next_round_robin_index = (index + 1) % len(sessions)
                return session_id

        session_id = self._session_name(len(self.session_user_counts))
        self.session_user_counts[session_id] = 0
        self.next_round_robin_index = 0
        return session_id

    def release_user(self, user_id: str) -> None:
        """Release a sticky assignment, for example on logout or expiry."""
        session_id = self.user_to_session.pop(user_id, None)
        if session_id is not None:
            self.session_user_counts[session_id] = max(0, self.session_user_counts[session_id] - 1)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--max-users-per-session", type=int, default=100)
    parser.add_argument(
        "--strategy",
        choices=["sticky-fill", "round-robin"],
        default="sticky-fill",
        help="Session assignment strategy for new users. Defaults to sticky-fill.",
    )
    parser.add_argument(
        "--pool-size",
        type=int,
        default=1,
        help="Initial logical session count for round-robin. No sessions are warmed or pre-created.",
    )
    args = parser.parse_args()

    project_endpoint = os.environ.get("FOUNDRY_PROJECT_ENDPOINT")
    if not project_endpoint:
        parser.error("Set FOUNDRY_PROJECT_ENDPOINT.")
    if not os.environ.get("AGENT_NAME"):
        parser.error("Set AGENT_NAME.")

    pool = SessionPool(
        max_users_per_session=args.max_users_per_session,
        strategy=args.strategy,
        pool_size=args.pool_size,
    )
    # This pool script owns assignment. The A-A-B helper below only receives
    # the chosen session ids and proves previous_response_id isolation for that
    # assignment.
    user_a_session = pool.get_session_for_user(ALICE_ID)
    user_b_session = pool.get_session_for_user(BOB_ID)

    print(f"Strategy: {args.strategy}")
    print("Pool assignments:")
    for user_id, session_id in sorted(pool.user_to_session.items()):
        print(f"{user_id} -> {session_id}")
    print()

    if user_a_session != user_b_session:
        print(
            "alice and bob landed in different sessions, so the same-session "
            "previous_response_id isolation check is not invoked."
        )
        return 0

    with AIProjectClient(
        endpoint=project_endpoint,
        credential=DefaultAzureCredential(),
        allow_preview=True,
    ) as project_client:
        result = run_previous_response_isolation(
            agent=HostedAgentResponsesClient(
                project_client.get_openai_client(agent_name=os.environ["AGENT_NAME"]).responses
            ),
            session_id=user_a_session,
            user_b_session_id=user_b_session,
            user_a=ALICE_ID,
            user_b=BOB_ID,
        )

    print_result(result)
    return 0


if __name__ == "__main__":
    sys.exit(main())
