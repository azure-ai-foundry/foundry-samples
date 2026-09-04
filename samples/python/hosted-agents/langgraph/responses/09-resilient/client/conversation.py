"""Linear conversation state and resilient turn execution."""

from __future__ import annotations

import asyncio
import json
from collections.abc import AsyncIterator
from contextlib import suppress
from dataclasses import dataclass, field
from time import monotonic
from typing import Any, Literal, cast
from uuid import uuid4

import httpx
from openai import (
    APIConnectionError,
    APIStatusError,
    AsyncOpenAI,
    AsyncStream,
    NotFoundError,
    OpenAIError,
)
from openai.types.responses import ResponseStreamEvent as OpenAIResponseStreamEvent

TERMINAL_STATUSES = {"completed", "failed", "cancelled", "incomplete"}
_RESPONSE_ID_HEADER = "x-agent-response-id"


def _new_response_id(partition_hint: str | None) -> str:
    """Create a canonical Agent Server response ID in the hinted partition."""
    partition_key: str | None = None
    if partition_hint:
        _, separator, body = partition_hint.partition("_")
        if separator and len(body) == 50:
            partition_key = body[:18]
        elif separator and len(body) == 48:
            partition_key = f"{body[-16:]}00"
    if partition_key is None:
        partition_key = f"{uuid4().hex[:16]}00"
    return f"caresp_{partition_key}{uuid4().hex}"


def _is_retryable_error(error: BaseException) -> bool:
    if isinstance(error, APIStatusError):
        if error.status_code == 424:
            return error.code == "session_not_ready"
        return 500 <= error.status_code < 600
    return isinstance(
        error,
        (APIConnectionError, httpx.TransportError, httpx.TimeoutException),
    )


class ConversationError(RuntimeError):
    """Raised when an action is invalid for the current conversation state."""


@dataclass(frozen=True)
class ApprovalRequest:
    """A sensitive tool call waiting for a human decision."""

    id: str
    action: str
    arguments: dict[str, Any]
    prompt: str


@dataclass(frozen=True)
class TurnSnapshot:
    """Immutable view of one conversation turn."""

    id: str
    user_text: str
    response_id: str | None
    status: str
    connection: Literal["creating", "streaming", "recovering", "terminal"]
    output_text: str
    steerable: bool
    approval: ApprovalRequest | None
    error: str | None


@dataclass(frozen=True)
class ConversationEvent:
    """A turn state change published to the UI."""

    turn: TurnSnapshot
    protocol_event: OpenAIResponseStreamEvent | None = None


@dataclass
class _Turn:
    id: str
    user_text: str
    request_input: Any
    parent_response_id: str | None
    response_id: str | None = None
    status: str = "queued"
    connection: Literal["creating", "streaming", "recovering", "terminal"] = "creating"
    output_chunks: list[str] = field(default_factory=list)
    steerable: bool = False
    approval: ApprovalRequest | None = None
    cursor: int | None = None
    error: str | None = None
    steered: bool = False
    cancellation_requested: bool = False

    def snapshot(self) -> TurnSnapshot:
        return TurnSnapshot(
            id=self.id,
            user_text=self.user_text,
            response_id=self.response_id,
            status=self.status,
            connection=self.connection,
            output_text="".join(self.output_chunks),
            steerable=self.steerable,
            approval=self.approval,
            error=self.error,
        )


class Conversation:
    """Run one automatic, linear Responses conversation."""

    def __init__(
        self,
        client: AsyncOpenAI,
        *,
        conversation_id: str | None = None,
        token_delay: float | None = None,
        reconnect_delay: float = 0.5,
        reconnect_timeout: float = 120.0,
    ) -> None:
        self._client = client
        self._conversation_id = conversation_id
        self._token_delay = token_delay
        self._reconnect_delay = reconnect_delay
        self._reconnect_timeout = reconnect_timeout
        self._turns: list[_Turn] = []
        self._current_turn: _Turn | None = None
        self._last_response_id: str | None = None
        self._events: asyncio.Queue[ConversationEvent] = asyncio.Queue()
        self._tasks: set[asyncio.Task[None]] = set()

    @property
    def turns(self) -> tuple[TurnSnapshot, ...]:
        """Return immutable snapshots of all turns."""
        return tuple(turn.snapshot() for turn in self._turns)

    @property
    def current_turn(self) -> TurnSnapshot | None:
        """Return the turn currently controlled by the composer."""
        return self._current_turn.snapshot() if self._current_turn else None

    async def next_event(self) -> ConversationEvent:
        """Wait for the next state change."""
        return await self._events.get()

    def send(self, text: str) -> TurnSnapshot:
        """Start a turn, automatically steering when output is active."""
        normalized = text.strip()
        if not normalized:
            raise ConversationError("Message cannot be empty")

        parent_response_id = self._last_response_id
        active = self._current_turn
        if active is not None and active.connection != "terminal":
            if active.response_id is None:
                raise ConversationError("Wait for the active response to be created")
            if not active.steerable:
                raise ConversationError("The active response does not support steering")
            parent_response_id = active.response_id
            active.steered = True

        return self._start_turn(
            user_text=normalized,
            request_input=normalized,
            parent_response_id=parent_response_id,
        )

    def approve_current(self) -> TurnSnapshot:
        """Approve the sensitive tool call on the current completed turn."""
        return self._decide_current(approve=True)

    def deny_current(self, reason: str = "") -> TurnSnapshot:
        """Reject the sensitive tool call on the current completed turn."""
        return self._decide_current(approve=False, reason=reason.strip())

    def _decide_current(
        self,
        *,
        approve: bool,
        reason: str = "",
    ) -> TurnSnapshot:
        turn = self._current_turn
        if turn is None or turn.connection != "terminal" or turn.approval is None:
            raise ConversationError("There is no tool call waiting for approval")
        if turn.response_id is None:
            raise ConversationError("The approval response has no response ID")

        approval = turn.approval
        decision: dict[str, Any] = {
            "type": "mcp_approval_response",
            "approval_request_id": approval.id,
            "approve": approve,
        }
        if reason:
            decision["reason"] = reason
        label = f"Approve {approval.action}" if approve else f"Deny {approval.action}"
        return self._start_turn(
            user_text=label,
            request_input=[decision],
            parent_response_id=turn.response_id,
        )

    def _start_turn(
        self,
        *,
        user_text: str,
        request_input: Any,
        parent_response_id: str | None,
    ) -> TurnSnapshot:
        turn = _Turn(
            id=_new_response_id(parent_response_id or self._conversation_id),
            user_text=user_text,
            request_input=request_input,
            parent_response_id=parent_response_id,
        )
        self._turns.append(turn)
        self._current_turn = turn
        self._publish(turn)
        task = asyncio.create_task(self._run_turn(turn))
        self._tasks.add(task)
        task.add_done_callback(self._tasks.discard)
        return turn.snapshot()

    async def cancel_current(self) -> None:
        """Request cancellation of the current response."""
        turn = self._current_turn
        if turn is None or turn.connection == "terminal":
            raise ConversationError("There is no active response to cancel")
        if turn.response_id is None:
            raise ConversationError("Wait for the active response to be created")
        turn.cancellation_requested = True
        try:
            await self._client.responses.cancel(turn.response_id)
        except Exception:
            turn.cancellation_requested = False
            raise

    async def close(self) -> None:
        """Cancel and await all local turn tasks."""
        tasks = tuple(self._tasks)
        for task in tasks:
            task.cancel()
        if tasks:
            await asyncio.gather(*tasks, return_exceptions=True)

    async def _run_turn(self, turn: _Turn) -> None:
        deadline = monotonic() + self._reconnect_timeout
        recovering = False
        detail: str | None = None
        try:
            while True:
                turn.connection = "recovering" if recovering else "creating"
                turn.error = detail
                self._publish(turn)
                try:
                    if recovering:
                        recovery_status = await self._recover_once(turn)
                        if recovery_status == "missing":
                            if monotonic() >= deadline:
                                self._fail(
                                    turn,
                                    detail or "Timed out reconnecting to response",
                                )
                                return
                            recovering = False
                            await asyncio.sleep(self._reconnect_delay)
                            continue
                        completed = recovery_status == "completed"
                    else:
                        completed = await self._stream_once(turn)

                    if completed:
                        return
                    if monotonic() >= deadline:
                        self._fail(
                            turn,
                            detail or "Timed out reconnecting to response",
                        )
                        return
                    recovering = True
                    detail = None
                    await asyncio.sleep(self._reconnect_delay)
                except (
                    OpenAIError,
                    httpx.TransportError,
                    httpx.TimeoutException,
                ) as exc:
                    detail = str(exc)
                    if isinstance(exc, APIStatusError):
                        with suppress(httpx.ResponseNotRead):
                            detail = exc.response.text or detail
                    if not _is_retryable_error(exc):
                        self._fail(turn, detail)
                        return
                    if monotonic() >= deadline:
                        self._fail(
                            turn,
                            f"Timed out reconnecting to response: {detail}",
                        )
                        return
                    recovering = True
                    turn.error = detail
                    self._publish(turn)
                    await asyncio.sleep(self._reconnect_delay)
                except Exception as exc:  # noqa: BLE001
                    self._fail(turn, str(exc))
                    return
        except asyncio.CancelledError:
            raise

    async def _stream_once(self, turn: _Turn) -> bool:
        stream = cast(
            AsyncStream[OpenAIResponseStreamEvent],
            await self._client.responses.create(**self._request(turn)),
        )
        await self._consume(turn, stream)
        return turn.connection == "terminal"

    async def _recover_once(
        self,
        turn: _Turn,
    ) -> Literal["completed", "pending", "missing"]:
        response_id = turn.response_id or turn.id
        try:
            if turn.cursor is None:
                stream = await self._client.responses.retrieve(
                    response_id,
                    stream=True,
                )
            else:
                stream = await self._client.responses.retrieve(
                    response_id,
                    starting_after=turn.cursor,
                    stream=True,
                )
            await self._consume(turn, stream)
        except NotFoundError:
            return "pending" if turn.response_id is not None else "missing"
        return "completed" if turn.connection == "terminal" else "pending"

    async def _consume(
        self,
        turn: _Turn,
        events: AsyncIterator[OpenAIResponseStreamEvent],
    ) -> None:
        async for openai_event in events:
            self._apply_event(turn, openai_event)
            self._publish(turn, openai_event)
            if turn.connection == "terminal":
                return

    def _apply_event(
        self,
        turn: _Turn,
        event: OpenAIResponseStreamEvent,
    ) -> None:
        payload = event.model_dump(mode="json")
        sequence_number = payload.get("sequence_number")
        if isinstance(sequence_number, int) and sequence_number >= 0:
            turn.cursor = sequence_number
        response = payload.get("response")
        if isinstance(response, dict):
            response_id = response.get("id") or response.get("response_id")
            if isinstance(response_id, str) and response_id:
                turn.response_id = response_id
            metadata = response.get("metadata")
            if isinstance(metadata, dict):
                turn.steerable = (
                    metadata.get("foundry.agent.steerable_conversation") == "true"
                )
            response_status = response.get("status")
            if isinstance(response_status, str) and response_status:
                turn.status = response_status

        delta = payload.get("delta")
        if event.type == "response.output_text.delta" and isinstance(delta, str):
            turn.output_chunks.append(delta)

        item = payload.get("item")
        if (
            event.type == "response.output_item.added"
            and isinstance(item, dict)
            and item.get("type") == "mcp_approval_request"
            and isinstance(item.get("id"), str)
        ):
            arguments = item.get("arguments")
            try:
                envelope = json.loads(arguments) if isinstance(arguments, str) else {}
            except json.JSONDecodeError:
                envelope = {}
            value = envelope.get("value")
            value = value if isinstance(value, dict) else {}
            tool_arguments = value.get("arguments")
            turn.approval = ApprovalRequest(
                id=item["id"],
                action=str(value.get("action") or "sensitive tool"),
                arguments=(tool_arguments if isinstance(tool_arguments, dict) else {}),
                prompt=str(value.get("prompt") or "Approve this tool call?"),
            )

        if event.type.startswith("response."):
            event_status = event.type.removeprefix("response.")
            if event_status in TERMINAL_STATUSES:
                if event_status == "failed" and turn.steered:
                    turn.status = "steering"
                elif event_status == "failed" and turn.cancellation_requested:
                    turn.status = "cancelled"
                else:
                    turn.status = event_status
                turn.connection = "terminal"
                turn.error = None
                if (
                    event_status == "completed"
                    and turn is self._current_turn
                    and turn.response_id is not None
                ):
                    self._last_response_id = turn.response_id
                return

        turn.connection = "streaming"
        turn.error = None

    def _request(self, turn: _Turn) -> dict[str, Any]:
        request: dict[str, Any] = {
            "input": turn.request_input,
            "background": True,
            "stream": True,
            "store": True,
            "extra_headers": {_RESPONSE_ID_HEADER: turn.id},
        }
        if self._conversation_id is not None:
            request["conversation"] = self._conversation_id
        if turn.parent_response_id is not None:
            request["previous_response_id"] = turn.parent_response_id
        if self._token_delay is not None:
            request["metadata"] = {"token_delay": str(self._token_delay)}
        return request

    def _fail(self, turn: _Turn, message: str) -> None:
        turn.status = "cancelled" if turn.cancellation_requested else "failed"
        turn.connection = "terminal"
        turn.error = None if turn.cancellation_requested else message
        self._publish(turn)

    def _publish(
        self,
        turn: _Turn,
        protocol_event: OpenAIResponseStreamEvent | None = None,
    ) -> None:
        self._events.put_nowait(ConversationEvent(turn.snapshot(), protocol_event))
