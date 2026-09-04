"""Linear Invocations conversation with durable same-session recovery."""

from __future__ import annotations

import asyncio
import json
from collections.abc import AsyncIterator
from contextlib import suppress
from dataclasses import dataclass, field
from time import monotonic
from typing import Any, Literal
from uuid import uuid4

import httpx


def _is_retryable_error(error: BaseException) -> bool:
    if isinstance(error, httpx.HTTPStatusError):
        if 500 <= error.response.status_code < 600:
            return True
        if error.response.status_code != 424:
            return False
        try:
            payload = error.response.json()
        except json.JSONDecodeError:
            return False
        response_error = payload.get("error") if isinstance(payload, dict) else None
        return (
            isinstance(response_error, dict)
            and response_error.get("code") == "session_not_ready"
        )
    return isinstance(
        error,
        (httpx.TransportError, httpx.TimeoutException),
    )


class ConversationError(RuntimeError):
    """Raised when an action is invalid for the current conversation state."""


@dataclass(frozen=True)
class ApprovalRequest:
    """A sensitive tool call waiting for a human decision."""

    id: str
    interrupt_id: str
    action: str
    arguments: dict[str, Any]
    prompt: str


@dataclass(frozen=True)
class TurnSnapshot:
    """Immutable view of one invocation turn."""

    id: str
    user_text: str
    status: str
    connection: Literal["sending", "polling", "waiting", "recovering", "terminal"]
    output_text: str
    approval: ApprovalRequest | None
    error: str | None
    accepted: bool


@dataclass(frozen=True)
class ConversationEvent:
    """A turn state change published to the UI."""

    turn: TurnSnapshot
    protocol_event: dict[str, Any] | None = None


@dataclass
class _Turn:
    id: str
    user_text: str
    request_message: str | list[dict[str, Any]]
    previous_invocation_id: str | None
    server_id: str | None = None
    status: str = "queued"
    connection: Literal[
        "sending", "polling", "waiting", "recovering", "terminal"
    ] = "sending"
    output_chunks: list[str] = field(default_factory=list)
    approval: ApprovalRequest | None = None
    error: str | None = None
    accepted: bool = False
    steered: bool = False
    cancellation_requested: bool = False
    steering_parent: _Turn | None = field(default=None, repr=False)
    admission_event: asyncio.Event = field(default_factory=asyncio.Event, repr=False)

    @property
    def invocation_id(self) -> str:
        return self.server_id or self.id

    def snapshot(self) -> TurnSnapshot:
        return TurnSnapshot(
            id=self.id,
            user_text=self.user_text,
            status=self.status,
            connection=self.connection,
            output_text="".join(self.output_chunks),
            approval=self.approval,
            error=self.error,
            accepted=self.accepted,
        )


class Conversation:
    """Run one Invocations session and retry interrupted turns in place."""

    def __init__(
        self,
        client: httpx.AsyncClient,
        invocations_url: str,
        *,
        session_id: str,
        reconnect_delay: float = 0.5,
        reconnect_timeout: float = 120.0,
    ) -> None:
        self._client = client
        self._invocations_url = httpx.URL(invocations_url)
        self._session_id = session_id
        self._reconnect_delay = reconnect_delay
        self._reconnect_timeout = reconnect_timeout
        self._turns: list[_Turn] = []
        self._current_turn: _Turn | None = None
        self._last_invocation_id: str | None = None
        self._events: asyncio.Queue[ConversationEvent] = asyncio.Queue()
        self._tasks: dict[str, asyncio.Task[None]] = {}

    @property
    def session_id(self) -> str:
        """Return the stable Agent Server session ID."""
        return self._session_id

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
        """Start a user turn, queueing behind an active invocation if needed."""
        normalized = text.strip()
        if not normalized:
            raise ConversationError("Message cannot be empty")

        previous_invocation_id = self._last_invocation_id
        steering_parent: _Turn | None = None
        active = self._current_turn
        if active is not None and active.connection != "terminal":
            previous_invocation_id = active.invocation_id
            steering_parent = active
            active.steered = True

        return self._start_turn(
            user_text=normalized,
            request_message=normalized,
            previous_invocation_id=previous_invocation_id,
            steering_parent=steering_parent,
        )

    def approve_current(self) -> TurnSnapshot:
        """Approve the sensitive tool call on the current turn."""
        return self._decide_current(approve=True)

    def deny_current(self) -> TurnSnapshot:
        """Reject the sensitive tool call on the current turn."""
        return self._decide_current(approve=False)

    def _decide_current(self, *, approve: bool) -> TurnSnapshot:
        turn = self._current_turn
        if turn is None or turn.connection != "terminal" or turn.approval is None:
            raise ConversationError("There is no tool call waiting for approval")
        if approve:
            decision = {
                "type": "mcp_approval_response",
                "approval_request_id": turn.approval.id,
                "approve": True,
            }
        else:
            decision = {
                "type": "function_call_output",
                "call_id": turn.approval.interrupt_id,
                "output": json.dumps({"resume": False}),
            }
        label = f"{'Approve' if approve else 'Deny'} {turn.approval.action}"
        return self._start_turn(
            user_text=label,
            request_message=[decision],
            previous_invocation_id=self._last_invocation_id,
        )

    async def cancel_current(self) -> None:
        """Request server-side cancellation and keep tracking the invocation."""
        turn = self._current_turn
        if turn is None or turn.connection == "terminal":
            raise ConversationError("There is no active invocation to cancel")
        task = self._tasks.get(turn.id)
        if task is None:
            raise ConversationError("The active invocation has no local task")

        await turn.admission_event.wait()
        if not turn.accepted or turn.connection == "terminal":
            raise ConversationError("There is no active invocation to cancel")

        cancellation_url = self._invocations_url.copy_with(
            path=(
                f"{self._invocations_url.path.rstrip('/')}/"
                f"{turn.invocation_id}/cancel"
            )
        ).copy_set_param("agent_session_id", self._session_id)
        try:
            response = await self._client.post(cancellation_url, timeout=None)
            if response.is_error:
                await response.aread()
            response.raise_for_status()
            payload = response.json()
        except (httpx.HTTPError, json.JSONDecodeError) as exc:
            raise ConversationError(f"Unable to cancel invocation: {exc}") from exc
        if not isinstance(payload, dict):
            raise ConversationError("Invocation cancellation returned an invalid payload")

        status = payload.get("status")
        if status not in {"cancelling", "cancelled", "completed", "failed"}:
            raise ConversationError(
                f"Invocation cancellation returned status {status!r}"
            )
        turn.cancellation_requested = status == "cancelling"
        turn.status = status
        turn.error = None
        self._publish(turn, {"event": "cancellation", "data": payload})

    async def close(self) -> None:
        """Cancel and await all local turn tasks."""
        tasks = tuple(self._tasks.values())
        for task in tasks:
            task.cancel()
        if tasks:
            await asyncio.gather(*tasks, return_exceptions=True)

    def _start_turn(
        self,
        *,
        user_text: str,
        request_message: str | list[dict[str, Any]],
        previous_invocation_id: str | None,
        steering_parent: _Turn | None = None,
    ) -> TurnSnapshot:
        turn = _Turn(
            id=f"inv_{uuid4().hex}{uuid4().hex[:18]}",
            user_text=user_text,
            request_message=request_message,
            previous_invocation_id=previous_invocation_id,
            steering_parent=steering_parent,
        )
        self._turns.append(turn)
        self._current_turn = turn
        self._publish(turn)
        task = asyncio.create_task(self._run_turn(turn))
        self._tasks[turn.id] = task
        task.add_done_callback(lambda _task: self._tasks.pop(turn.id, None))
        return turn.snapshot()

    async def _run_turn(self, turn: _Turn) -> None:
        recovery_deadline: float | None = None
        recovering = False
        waiting_for_session = False
        admitted = False
        try:
            parent = turn.steering_parent
            if parent is not None:
                await parent.admission_event.wait()
                if not parent.accepted:
                    self._fail(
                        turn,
                        "Cannot steer because the previous invocation was not accepted",
                    )
                    return
                turn.previous_invocation_id = parent.invocation_id

            while True:
                turn.connection = (
                    "waiting"
                    if recovering and waiting_for_session
                    else "recovering"
                    if recovering
                    else "polling"
                    if admitted
                    else "sending"
                )
                if recovering:
                    turn.status = "waiting" if waiting_for_session else "recovering"
                elif not admitted:
                    turn.status = "in_progress"
                self._publish(turn)
                try:
                    if (
                        recovery_deadline is not None
                        and monotonic() >= recovery_deadline
                    ):
                        self._fail(
                            turn,
                            f"Timed out retrying invocation: {turn.error}",
                        )
                        return
                    if recovering:
                        recovery_status = await self._recover_once(turn)
                        if recovery_status == "active":
                            recovering = False
                            waiting_for_session = False
                            recovery_deadline = None
                            turn.error = None
                            await asyncio.sleep(self._reconnect_delay)
                            continue
                        if recovery_status == "unavailable":
                            await asyncio.sleep(self._reconnect_delay)
                            continue
                        if recovery_status == "missing":
                            recovering = False
                            waiting_for_session = False
                            admitted = False
                            turn.accepted = False
                            await asyncio.sleep(self._reconnect_delay)
                            continue
                        if recovery_status in {"cancelled", "steered"}:
                            return
                    elif not admitted:
                        await self._start_background_once(turn)
                        admitted = True
                        recovery_deadline = None
                        await asyncio.sleep(self._reconnect_delay)
                        continue

                    if not recovering:
                        recovery_status = await self._recover_once(turn)
                        if recovery_status == "active":
                            await asyncio.sleep(self._reconnect_delay)
                            continue
                        if recovery_status == "unavailable":
                            if recovery_deadline is None:
                                recovery_deadline = (
                                    monotonic() + self._reconnect_timeout
                                )
                            recovering = True
                            waiting_for_session = True
                            turn.error = "Invocation is temporarily unavailable"
                            await asyncio.sleep(self._reconnect_delay)
                            continue
                        if recovery_status == "missing":
                            admitted = False
                            turn.accepted = False
                            await asyncio.sleep(self._reconnect_delay)
                            continue
                        if recovery_status in {"cancelled", "steered"}:
                            return

                    if turn is self._current_turn:
                        self._last_invocation_id = turn.invocation_id
                    turn.cancellation_requested = False
                    turn.status = "approval_required" if turn.approval else "completed"
                    turn.connection = "terminal"
                    turn.error = None
                    self._publish(turn)
                    return
                except (
                    httpx.HTTPStatusError,
                    httpx.TransportError,
                    httpx.TimeoutException,
                ) as exc:
                    detail = str(exc)
                    if isinstance(exc, httpx.HTTPStatusError):
                        with suppress(httpx.ResponseNotRead):
                            detail = exc.response.text or detail
                    if not _is_retryable_error(exc):
                        self._fail(turn, detail)
                        return
                    if recovery_deadline is None:
                        recovery_deadline = monotonic() + self._reconnect_timeout
                    recovering = True
                    waiting_for_session = admitted or (
                        isinstance(exc, httpx.HTTPStatusError)
                        and exc.response.status_code == 424
                    )
                    turn.error = detail
                    self._publish(turn)
                    await asyncio.sleep(self._reconnect_delay)
                except (ConversationError, json.JSONDecodeError) as exc:
                    self._fail(turn, str(exc))
                    return
        except asyncio.CancelledError:
            turn.status = "cancelled"
            turn.connection = "terminal"
            turn.error = None
            turn.admission_event.set()
            self._publish(turn)
            raise

    async def _start_background_once(self, turn: _Turn) -> None:
        request_body: dict[str, Any] = {
            "message": turn.request_message,
            "background": True,
        }
        if turn.previous_invocation_id is not None:
            request_body["previous_invocation_id"] = turn.previous_invocation_id
        response = await self._client.post(
            self._invocations_url.copy_set_param("agent_session_id", self._session_id),
            headers={"x-agent-invocation-id": turn.invocation_id},
            json=request_body,
            timeout=None,
        )
        if response.is_error:
            await response.aread()
        response.raise_for_status()
        payload = response.json()
        if not isinstance(payload, dict):
            raise ConversationError("Invocation create returned an invalid payload")

        response_invocation_id = payload.get("id")
        if not isinstance(response_invocation_id, str) or not response_invocation_id:
            response_invocation_id = response.headers.get("x-agent-invocation-id")
        if isinstance(response_invocation_id, str) and response_invocation_id:
            turn.server_id = response_invocation_id

        status = payload.get("status")
        if status not in {"queued", "in_progress"}:
            raise ConversationError(
                f"Invocation create returned status {status!r} instead of queued or in_progress"
            )
        turn.accepted = True
        self._last_invocation_id = turn.invocation_id
        turn.connection = "polling"
        turn.status = status
        turn.error = None
        self._publish(turn, {"event": "accepted", "data": payload})
        turn.admission_event.set()

    async def _recover_once(
        self,
        turn: _Turn,
    ) -> Literal[
        "completed", "active", "unavailable", "missing", "cancelled", "steered"
    ]:
        invocation_url = self._invocations_url.copy_with(
            path=f"{self._invocations_url.path.rstrip('/')}/{turn.invocation_id}"
        ).copy_set_param("agent_session_id", self._session_id)
        response = await self._client.get(
            invocation_url,
            timeout=None,
        )
        if response.status_code == 404:
            return "unavailable" if turn.server_id is not None else "missing"
        response.raise_for_status()
        payload = response.json()
        if not isinstance(payload, dict):
            raise ConversationError("Invocation recovery returned an invalid payload")

        status = payload.get("status")
        if status in {
            "queued",
            "in_progress",
            "completed",
            "failed",
            "cancelled",
        } and not turn.accepted:
            response_invocation_id = payload.get("id")
            if isinstance(response_invocation_id, str) and response_invocation_id:
                turn.server_id = response_invocation_id
            turn.accepted = True
            self._last_invocation_id = turn.invocation_id
            turn.admission_event.set()
        if status in {"queued", "in_progress"}:
            turn.status = "cancelling" if turn.cancellation_requested else status
            self._publish(turn, {"event": "recovery", "data": payload})
            return "active"
        if status == "completed":
            response_text = payload.get("response")
            if isinstance(response_text, str):
                streamed_text = "".join(turn.output_chunks)
                if response_text.startswith(streamed_text):
                    recovered_text = response_text[len(streamed_text) :]
                elif streamed_text:
                    recovered_text = f"\n{response_text}"
                else:
                    recovered_text = response_text
                turn.output_chunks[:] = [response_text]
                if recovered_text:
                    self._publish(
                        turn,
                        {"event": "message", "data": {"token": recovered_text}},
                    )
            turn.approval = None
            for item in payload.get("output") or []:
                if isinstance(item, dict):
                    self._apply_output_item(turn, item)
                    self._publish(turn, {"event": "output_item", "data": item})
            return "completed"
        if status == "cancelled":
            error = payload.get("error")
            error_code = error.get("code") if isinstance(error, dict) else None
            if turn.steered or error_code == "steered":
                turn.status = "steering"
                turn.connection = "terminal"
                turn.error = None
                self._publish(turn)
                return "steered"
            turn.cancellation_requested = False
            turn.status = "cancelled"
            turn.connection = "terminal"
            turn.approval = None
            turn.error = None
            self._publish(turn, {"event": "recovery", "data": payload})
            return "cancelled"
        if status == "failed":
            error = payload.get("error")
            if isinstance(error, dict):
                error = error.get("message") or error.get("code")
            raise ConversationError(str(error or f"Invocation {status}"))
        raise ConversationError(f"Invocation recovery returned status {status!r}")

    @staticmethod
    def _apply_output_item(turn: _Turn, data: dict[str, Any]) -> None:
        if data.get("type") != "mcp_approval_request":
            return
        envelope = json.loads(str(data.get("arguments") or "{}"))
        interrupt_id = envelope.get("interrupt_id")
        if not isinstance(interrupt_id, str) or not interrupt_id:
            raise ConversationError(
                "Approval item is missing its LangGraph interrupt ID"
            )
        value = envelope.get("value")
        value = value if isinstance(value, dict) else {}
        arguments = value.get("arguments")
        turn.approval = ApprovalRequest(
            id=str(data.get("id") or "approval"),
            interrupt_id=interrupt_id,
            action=str(value.get("action") or "sensitive tool"),
            arguments=arguments if isinstance(arguments, dict) else {},
            prompt=str(value.get("prompt") or "Approve this tool call?"),
        )

    def _fail(self, turn: _Turn, message: str) -> None:
        turn.status = "failed"
        turn.connection = "terminal"
        turn.error = message
        turn.admission_event.set()
        parent = turn.steering_parent
        if (
            not turn.accepted
            and self._current_turn is turn
            and parent is not None
            and parent.connection != "terminal"
        ):
            parent.steered = False
            self._current_turn = parent
        self._publish(turn)
        if parent is not None and self._current_turn is parent:
            self._publish(parent)

    def _publish(
        self,
        turn: _Turn,
        protocol_event: dict[str, Any] | None = None,
    ) -> None:
        self._events.put_nowait(ConversationEvent(turn.snapshot(), protocol_event))


async def _iter_sse_events(
    lines: AsyncIterator[str],
) -> AsyncIterator[tuple[str, dict[str, Any]]]:
    event_type = "message"
    data_lines: list[str] = []
    async for line in lines:
        if not line:
            if data_lines:
                raw_data = "\n".join(data_lines)
                payload = json.loads(raw_data)
                if not isinstance(payload, dict):
                    payload = {"value": payload}
                yield event_type, payload
            event_type = "message"
            data_lines = []
        elif line.startswith("event:"):
            event_type = line.split(":", 1)[1].strip() or "message"
        elif line.startswith("data:"):
            data_lines.append(line.split(":", 1)[1].lstrip())

    if data_lines:
        payload = json.loads("\n".join(data_lines))
        if not isinstance(payload, dict):
            payload = {"value": payload}
        yield event_type, payload
