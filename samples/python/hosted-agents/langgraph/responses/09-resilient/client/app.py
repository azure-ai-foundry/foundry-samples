"""Textual interface for resilient Responses conversations."""

from __future__ import annotations

import json
from dataclasses import dataclass

from textual import on
from textual.app import App, ComposeResult
from textual.binding import Binding
from textual.containers import Horizontal, Vertical
from textual.message import Message
from textual.widgets import Button, Footer, Header, Static, TextArea

from conversation import (
    Conversation,
    ConversationError,
    ConversationEvent,
    TurnSnapshot,
)


class ConversationChanged(Message):
    """Deliver one conversation state change to the Textual app loop."""

    def __init__(self, event: ConversationEvent) -> None:
        super().__init__()
        self.event = event


class WrappingLog(TextArea):
    """Read-only, wrapping text output with append-style writes."""

    def __init__(self, *, id: str) -> None:
        super().__init__(
            id=id,
            soft_wrap=True,
            read_only=True,
            show_cursor=False,
            show_line_numbers=False,
            highlight_cursor_line=False,
            compact=True,
            max_checkpoints=0,
        )

    def write(self, text: str) -> None:
        result = self.insert(text, self.document.end)
        self.move_cursor(result.end_location)
        self.scroll_end(animate=False, x_axis=False)


class WrappingComposer(TextArea):
    """Soft-wrapping composer that submits on Enter."""

    BINDINGS = [Binding("enter", "submit", show=False, priority=True)]

    @dataclass
    class Submitted(Message):
        composer: WrappingComposer
        value: str

        @property
        def control(self) -> WrappingComposer:
            return self.composer

    def __init__(self, *, id: str) -> None:
        super().__init__(
            id=id,
            placeholder="Send a message",
            soft_wrap=True,
            show_line_numbers=False,
            highlight_cursor_line=False,
            compact=True,
        )

    @property
    def value(self) -> str:
        return self.text

    @value.setter
    def value(self, text: str) -> None:
        self.load_text(text)

    def action_submit(self) -> None:
        self.post_message(self.Submitted(self, self.text))


class ResponsesCuiApp(App[None]):
    """Interactive full-screen client for one resilient conversation."""

    TITLE = "Resilient Responses"
    SUB_TITLE = "LangGraph long-running agent"
    MESSAGE_SEPARATOR = "----------------------------------------"
    BINDINGS = [
        ("ctrl+c", "interrupt", "Cancel / Exit"),
        ("ctrl+r", "toggle_raw", "Raw events"),
        ("ctrl+q", "quit", "Quit"),
        Binding("left", "focus_approve", show=False, priority=True),
        Binding("right", "focus_deny", show=False, priority=True),
    ]
    CSS = """
    Screen {
        background: #101513;
        color: #d8e0dc;
    }

    Header, Footer {
        background: #18211d;
        color: #d8e0dc;
    }

    #workspace {
        height: 1fr;
        padding: 1 2;
    }

    #status {
        height: 2;
        color: #88c999;
    }

    #conversation-area, #raw-events {
        height: 1fr;
        border: solid #34473e;
        background: #0c110f;
    }

    #transcript {
        height: 1fr;
        padding: 1 2;
    }

    #approval-actions {
        display: none;
        height: 4;
        padding: 0 1;
        border-top: solid #34473e;
        align: right middle;
    }

    #approval-actions.visible {
        display: block;
    }

    #approval-label {
        width: 1fr;
        content-align: left middle;
        color: #e4bd6b;
    }

    #raw-events {
        display: none;
        color: #9db4a8;
    }

    #raw-events.visible {
        display: block;
    }

    #composer-row {
        height: 4;
        margin-top: 1;
    }

    #composer {
        width: 1fr;
        margin-right: 1;
    }

    #send, #cancel {
        min-width: 10;
        margin-left: 1;
    }

    #approve, #deny {
        min-width: 12;
        margin-left: 1;
    }

    Button.-primary {
        background: #2f8050;
    }

    Button.-error {
        background: #8c4b43;
    }
    """

    def __init__(self, conversation: Conversation) -> None:
        super().__init__()
        self._conversation = conversation
        self._seen_turns: set[str] = set()
        self._assistant_items: dict[str, set[str]] = {}
        self._tool_items: dict[str, set[str]] = {}
        self._turn_connections: dict[str, str] = {}
        self._focused_approval_id: str | None = None
        self._pending_approval_decision: tuple[str, bool] | None = None
        self._exit_armed = False

    def compose(self) -> ComposeResult:
        yield Header()
        with Vertical(id="workspace"):
            yield Static("Ready", id="status")
            with Vertical(id="conversation-area"):
                yield WrappingLog(id="transcript")
                with Horizontal(id="approval-actions"):
                    yield Static("Sensitive tool call requires approval", id="approval-label")
                    yield Button("Approve", id="approve", variant="success")
                    yield Button("Deny", id="deny", variant="warning")
            yield WrappingLog(id="raw-events")
            with Horizontal(id="composer-row"):
                yield WrappingComposer(id="composer")
                yield Button("Send", id="send", variant="primary")
                yield Button("Cancel", id="cancel", variant="error", disabled=True)
        yield Footer()

    def on_mount(self) -> None:
        self.query_one("#composer", WrappingComposer).focus()
        self.run_worker(
            self._consume_session_events(),
            name="session-events",
            group="session",
            exclusive=True,
        )

    async def on_unmount(self) -> None:
        await self._conversation.close()

    @on(WrappingComposer.Submitted, "#composer")
    async def submit_from_input(self, event: WrappingComposer.Submitted) -> None:
        await self._send(event.value)

    @on(Button.Pressed, "#send")
    async def submit_from_button(self) -> None:
        composer = self.query_one("#composer", WrappingComposer)
        await self._send(composer.value)

    @on(Button.Pressed, "#cancel")
    async def cancel_from_button(self) -> None:
        await self.action_cancel_response()

    @on(Button.Pressed, "#approve")
    async def approve_from_button(self) -> None:
        await self._send_approval(approve=True)

    @on(Button.Pressed, "#deny")
    async def deny_from_button(self) -> None:
        await self._send_approval(approve=False)

    def action_focus_approve(self) -> None:
        self._focus_approval_button("#approve")

    def action_focus_deny(self) -> None:
        self._focus_approval_button("#deny")

    def _focus_approval_button(self, selector: str) -> None:
        approval_actions = self.query_one("#approval-actions", Horizontal)
        button = self.query_one(selector, Button)
        if approval_actions.has_class("visible") and not button.disabled:
            button.focus()

    def action_interrupt(self) -> None:
        if self._exit_armed:
            self.exit()
            return

        self._exit_armed = True
        self.set_timer(2.0, self._reset_exit_armed)
        self.notify("Press Ctrl+C again to exit.")
        self.run_worker(
            self._request_interrupt_cancellation(),
            name="interrupt-cancel",
            group="interrupt",
            exclusive=True,
            exit_on_error=False,
        )

    async def _request_interrupt_cancellation(self) -> None:
        try:
            await self._conversation.cancel_current()
            self.notify("Cancellation requested.")
        except ConversationError:
            return

    async def action_cancel_response(self) -> None:
        try:
            await self._conversation.cancel_current()
            self.notify("Cancellation requested")
        except ConversationError as exc:
            self.notify(str(exc), severity="warning")

    async def _send_approval(self, *, approve: bool) -> None:
        current = self._conversation.current_turn
        if current is not None and current.approval is not None:
            if current.connection != "terminal":
                self._pending_approval_decision = (current.approval.id, approve)
                self.query_one("#approval-actions", Horizontal).remove_class("visible")
                self.query_one("#approve", Button).disabled = True
                self.query_one("#deny", Button).disabled = True
                self.query_one("#status", Static).update("Approval queued...")
                return
        self._submit_approval(approve=approve)

    def _submit_approval(self, *, approve: bool) -> None:
        try:
            if approve:
                self._conversation.approve_current()
            else:
                self._conversation.deny_current()
        except ConversationError as exc:
            self.notify(str(exc), severity="warning")
            return
        self.query_one("#approval-actions", Horizontal).remove_class("visible")
        self.query_one("#approve", Button).disabled = True
        self.query_one("#deny", Button).disabled = True

    def action_toggle_raw(self) -> None:
        self.query_one("#raw-events", WrappingLog).toggle_class("visible")

    def _reset_exit_armed(self) -> None:
        self._exit_armed = False

    async def _send(self, value: str) -> None:
        try:
            self._conversation.send(value)
        except ConversationError as exc:
            self.notify(str(exc), severity="warning")
            return
        composer = self.query_one("#composer", WrappingComposer)
        composer.value = ""
        self.query_one("#send", Button).disabled = True

    async def _consume_session_events(self) -> None:
        while True:
            event = await self._conversation.next_event()
            self.post_message(ConversationChanged(event))

    @on(ConversationChanged)
    def render_conversation_change(self, message: ConversationChanged) -> None:
        event = message.event
        turn = event.turn
        transcript = self.query_one("#transcript", WrappingLog)
        if turn.id not in self._seen_turns:
            self._seen_turns.add(turn.id)
            transcript.write(
                f"\nYou: {turn.user_text}\n\n"
                f"{self.MESSAGE_SEPARATOR}\n\n"
                "Assistant: "
            )

        protocol_event = event.protocol_event
        if protocol_event is not None:
            payload = protocol_event.model_dump(mode="json")
            self.query_one("#raw-events", WrappingLog).write(
                f"{protocol_event.type} {payload}\n"
            )
            item = payload.get("item")
            if (
                protocol_event.type == "response.output_item.added"
                and isinstance(item, dict)
                and item.get("type") == "mcp_approval_request"
                and turn.approval is not None
            ):
                transcript.write(
                    "\n[Approval required]\n"
                    f"Action: {turn.approval.action}\n"
                    f"Arguments: {json.dumps(turn.approval.arguments, sort_keys=True)}\n"
                )
            if (
                protocol_event.type == "response.output_item.added"
                and isinstance(item, dict)
                and item.get("type") == "function_call"
                and isinstance(item.get("id"), str)
                and isinstance(item.get("name"), str)
            ):
                seen_tools = self._tool_items.setdefault(turn.id, set())
                item_id = item["id"]
                if item_id not in seen_tools:
                    seen_tools.add(item_id)
                    transcript.write(f"\n[Tool: {item['name']}]\n")
            if (
                protocol_event.type == "response.output_item.added"
                and isinstance(item, dict)
                and item.get("type") == "message"
                and isinstance(item.get("id"), str)
            ):
                seen_items = self._assistant_items.setdefault(turn.id, set())
                item_id = item["id"]
                if item_id not in seen_items:
                    if seen_items:
                        transcript.write("\n\n")
                    seen_items.add(item_id)
            delta = payload.get("delta")
            if protocol_event.type == "response.output_text.delta" and isinstance(
                delta, str
            ):
                transcript.write(delta)

        previous_connection = self._turn_connections.get(turn.id)
        if turn.connection != previous_connection:
            self._turn_connections[turn.id] = turn.connection
            if turn.connection == "recovering":
                transcript.write("\n[Connection lost. Retrying response...]\n")
            elif turn.connection == "terminal":
                transcript.write(f"\n[{turn.status}]\n")
                if turn.error:
                    transcript.write(f"{turn.error}\n")

        current = self._conversation.current_turn
        if current is not None and current.id == turn.id:
            self._update_current_controls(current)

    def _update_current_controls(self, turn: TurnSnapshot) -> None:
        status_text = {
            "creating": "Starting response...",
            "streaming": "Receiving output...",
            "recovering": "Receiving output...",
            "terminal": turn.status.capitalize(),
        }[turn.connection]
        self.query_one("#status", Static).update(status_text)
        can_send = turn.connection == "terminal" or (
            turn.response_id is not None and turn.steerable
        )
        self.query_one("#send", Button).disabled = not can_send
        self.query_one("#cancel", Button).disabled = (
            turn.connection == "terminal" or turn.response_id is None
        )
        approval_id = turn.approval.id if turn.approval is not None else None
        pending_decision = self._pending_approval_decision
        if (
            turn.connection == "terminal"
            and approval_id is not None
            and pending_decision is not None
            and pending_decision[0] == approval_id
        ):
            self._pending_approval_decision = None
            self._submit_approval(approve=pending_decision[1])
            return

        awaiting_approval = approval_id is not None and pending_decision is None
        self.query_one("#approval-actions", Horizontal).set_class(
            awaiting_approval, "visible"
        )
        approve_button = self.query_one("#approve", Button)
        approve_button.disabled = not awaiting_approval
        self.query_one("#deny", Button).disabled = not awaiting_approval
        if awaiting_approval:
            self.query_one("#status", Static).update("Approval required")
            if approval_id != self._focused_approval_id:
                self._focused_approval_id = approval_id
                approve_button.focus()
