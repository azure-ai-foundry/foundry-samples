# Copyright (c) Microsoft. All rights reserved.

"""Hotel booking agent using azure-ai-agentserver-invocations.

A voice + click demo agent that handles:
- Voice: natural language hotel search, questions, and modifications
- Click: selecting a hotel from results, confirming a booking

The agent maintains per-session state (search results, selected hotel,
booking status) so multi-turn conversations work naturally.

Voice Live sends two kinds of input:
  1. {"type": "input_audio.transcription", "input": "..."} — speech
  2. Arbitrary JSON from response.create invoke_input — click/UI events

The agent returns SSE streams with:
  - output_audio_transcription.delta — text to be spoken by TTS
  - output_audio_transcription.done — marks speech complete
  - Custom typed events — passed through to the client as-is (UI cards, etc.)
  - done — marks the invocation complete
"""

import asyncio
import json
import logging
import re
from collections import defaultdict

from starlette.requests import Request
from starlette.responses import Response, StreamingResponse

from azure.ai.agentserver.invocations import InvocationAgentServerHost

logger = logging.getLogger(__name__)

app = InvocationAgentServerHost()

# ---------------------------------------------------------------------------
# In-memory session store (replace with Redis/Cosmos DB for production)
# ---------------------------------------------------------------------------

_sessions: dict[str, dict] = defaultdict(lambda: {
    "state": "idle",          # idle | results_shown | hotel_selected | booked
    "search_query": None,
    "results": [],
    "selected_hotel": None,
    "booking": None,
})

# ---------------------------------------------------------------------------
# Hotel "database"
# ---------------------------------------------------------------------------

HOTELS = [
    {
        "id": "grand-foundry",
        "name": "The Grand Foundry",
        "price": 200,
        "rating": 4.5,
        "features": ["rooftop bar", "spa", "city view"],
    },
    {
        "id": "microsoft-downtown",
        "name": "Microsoft Downtown",
        "price": 180,
        "rating": 4.3,
        "features": ["pool", "gym", "free breakfast"],
    },
    {
        "id": "voice-waterfront",
        "name": "Voice Waterfront",
        "price": 220,
        "rating": 4.6,
        "features": ["waterfront view", "restaurant", "concierge"],
    },
]


def _search_hotels(query: str) -> list[dict]:
    """Simple keyword search over the hotel database."""
    q = query.lower()
    # If specific features mentioned, filter; otherwise return all
    matched = []
    for h in HOTELS:
        features_str = " ".join(h["features"])
        if any(kw in q for kw in h["name"].lower().split()) or any(kw in q for kw in features_str.split()):
            matched.append(h)
    return matched if matched else list(HOTELS)


def _format_hotel_list(hotels: list[dict]) -> str:
    """Format hotels into speakable text."""
    lines = []
    for i, h in enumerate(hotels, 1):
        lines.append(
            f"Option {i}: {h['name']}, ${h['price']} per night, "
            f"rated {h['rating']} stars, with {', '.join(h['features'])}."
        )
    return " ".join(lines)


# ---------------------------------------------------------------------------
# SSE helpers
# ---------------------------------------------------------------------------

async def _stream_speech_and_events(speech_text: str, events: list[dict]):
    """Yield SSE: word-by-word speech deltas, then custom events, then done."""
    words = speech_text.split()
    for word in words:
        evt = {"type": "output_audio_transcription.delta", "delta": word + " "}
        yield f"data: {json.dumps(evt)}\n\n"
        await asyncio.sleep(0.03)

    yield f'data: {json.dumps({"type": "output_audio_transcription.done", "text": speech_text})}\n\n'

    for evt in events:
        yield f"data: {json.dumps(evt)}\n\n"

    yield 'data: {"type": "done"}\n\n'


def _sse_response(speech_text: str, events: list[dict] | None = None) -> StreamingResponse:
    return StreamingResponse(
        _stream_speech_and_events(speech_text, events or []),
        media_type="text/event-stream",
        headers={"Cache-Control": "no-cache", "X-Accel-Buffering": "no"},
    )


# ---------------------------------------------------------------------------
# Intent detection (simple keyword-based; swap for LLM in production)
# ---------------------------------------------------------------------------

_BOOK_PATTERN = re.compile(r"book|reserve|find.*hotel|hotel.*in|stay.*in|looking for.*hotel", re.IGNORECASE)
_SELECT_PATTERN = re.compile(r"(option|number|the)\s*(\d)", re.IGNORECASE)
_CONFIRM_PATTERN = re.compile(r"yes|confirm|go ahead|book it|sounds good", re.IGNORECASE)
_CANCEL_PATTERN = re.compile(r"cancel|never\s?mind|start over", re.IGNORECASE)
_CHANGE_PATTERN = re.compile(r"change|modify|different|another|late.*checkout|early.*checkin", re.IGNORECASE)


# ---------------------------------------------------------------------------
# Request handler
# ---------------------------------------------------------------------------

@app.invoke_handler
async def handle_invoke(request: Request) -> Response:
    data = await request.json()
    session_id = request.state.session_id
    session = _sessions[session_id]

    input_type = data.get("type", "")

    # --- Path A: speech input ---
    if input_type == "input_audio.transcription":
        user_text = data.get("input", "").strip()
        logger.info("Session %s | speech: %s", session_id, user_text)
        return _handle_speech(session, user_text)

    # --- Path B: click / UI input ---
    action = data.get("action", "")
    logger.info("Session %s | click: type=%s action=%s", session_id, input_type, action)
    return _handle_click(session, data)


# ---------------------------------------------------------------------------
# Speech handler
# ---------------------------------------------------------------------------

def _handle_speech(session: dict, text: str) -> StreamingResponse:
    state = session["state"]

    # Cancel / reset
    if _CANCEL_PATTERN.search(text):
        session.update(state="idle", search_query=None, results=[], selected_hotel=None, booking=None)
        return _sse_response("No problem, I've cleared everything. How can I help you?")

    # Search for hotels
    if state == "idle" and _BOOK_PATTERN.search(text):
        results = _search_hotels(text)
        session["results"] = results
        session["search_query"] = text
        session["state"] = "results_shown"

        speech = f"I found {len(results)} hotels. " + _format_hotel_list(results) + " Which one would you like?"
        ui_events = [
            {
                "type": "ui.hotel_cards",
                "hotels": [
                    {"id": h["id"], "name": h["name"], "price": h["price"], "rating": h["rating"]}
                    for h in results
                ],
            },
            {
                "type": "ui.action_buttons",
                "actions": [
                    {"label": f"Select {h['name'].split()[0]}", "action": "select_hotel", "hotel_id": h["id"]}
                    for h in results
                ],
            },
        ]
        return _sse_response(speech, ui_events)

    # Voice-select from results: "option 2", "the second one"
    if state == "results_shown":
        m = _SELECT_PATTERN.search(text)
        if m:
            idx = int(m.group(2)) - 1
            if 0 <= idx < len(session["results"]):
                return _select_hotel(session, session["results"][idx])
            return _sse_response(f"Sorry, I only have {len(session['results'])} options. Which one?")

        # Might also say the hotel name
        for h in session["results"]:
            if h["name"].lower().split()[0] in text.lower():
                return _select_hotel(session, h)

        return _sse_response("Which hotel would you like? You can say the option number or tap to select.")

    # Confirm booking via voice
    if state == "hotel_selected" and _CONFIRM_PATTERN.search(text):
        return _confirm_booking(session)

    # Modification request
    if state in ("hotel_selected", "booked") and _CHANGE_PATTERN.search(text):
        hotel = session["selected_hotel"]
        # Simple: acknowledge the change
        if "late" in text.lower() and "checkout" in text.lower():
            speech = f"Sure, I've added late checkout to your reservation at {hotel['name']}. The updated total is ${hotel['price'] * 2 + 35} for two nights."
            return _sse_response(speech, [{"type": "ui.booking_update", "change": "late_checkout", "surcharge": 35}])
        return _sse_response("What would you like to change?")

    # Post-booking or default
    if state == "booked":
        return _sse_response(
            "Your booking is confirmed! Is there anything else I can help with? "
            "You can say 'start over' to search for another hotel."
        )

    # Default / greeting
    return _sse_response(
        "Welcome! I can help you find and book a hotel. "
        "Just say something like 'Find me a hotel in Seattle' to get started."
    )


# ---------------------------------------------------------------------------
# Click handler
# ---------------------------------------------------------------------------

def _handle_click(session: dict, data: dict) -> StreamingResponse:
    action = data.get("action", "")

    if action == "select_hotel":
        hotel_id = data.get("hotel_id", "")
        hotel = next((h for h in session.get("results", []) if h["id"] == hotel_id), None)
        if hotel:
            return _select_hotel(session, hotel)
        return _sse_response("I couldn't find that hotel. Could you try again?")

    if action == "confirm_booking":
        if session["state"] == "hotel_selected":
            return _confirm_booking(session)
        return _sse_response("There's no hotel selected yet. Would you like to search for one?")

    if action == "cancel":
        session.update(state="idle", search_query=None, results=[], selected_hotel=None, booking=None)
        return _sse_response("Booking cancelled. How else can I help?")

    return _sse_response("I'm not sure what that action means. Could you try again?")


# ---------------------------------------------------------------------------
# Shared business logic
# ---------------------------------------------------------------------------

def _select_hotel(session: dict, hotel: dict) -> StreamingResponse:
    session["selected_hotel"] = hotel
    session["state"] = "hotel_selected"
    nights = 2
    total = hotel["price"] * nights
    speech = (
        f"Great choice! {hotel['name']}, ${hotel['price']} per night for {nights} nights, "
        f"totaling ${total}. Would you like me to book it? "
        "You can say 'yes' or tap Confirm Booking."
    )
    ui_events = [
        {
            "type": "ui.hotel_detail",
            "hotel": {
                "id": hotel["id"],
                "name": hotel["name"],
                "price": hotel["price"],
                "nights": nights,
                "total": total,
                "features": hotel["features"],
            },
        },
        {
            "type": "ui.action_buttons",
            "actions": [
                {"label": "Confirm Booking", "action": "confirm_booking"},
                {"label": "Cancel", "action": "cancel"},
            ],
        },
    ]
    return _sse_response(speech, ui_events)


def _confirm_booking(session: dict) -> StreamingResponse:
    hotel = session["selected_hotel"]
    if not hotel:
        return _sse_response("No hotel selected. Let's search for one first.")

    confirmation_code = f"HTL-{hotel['id'][:3].upper()}-{hash(hotel['id']) % 10000:04d}"
    nights = 2
    total = hotel["price"] * nights
    session["booking"] = {"confirmation_code": confirmation_code, "hotel": hotel, "nights": nights, "total": total}
    session["state"] = "booked"

    speech = (
        f"All set! Your booking at {hotel['name']} is confirmed. "
        f"Confirmation code: {confirmation_code}. "
        f"Total: ${total} for {nights} nights. "
        "A confirmation has been sent to your email. Is there anything else?"
    )
    ui_events = [
        {
            "type": "ui.booking_confirmed",
            "booking": {
                "confirmation_code": confirmation_code,
                "hotel_name": hotel["name"],
                "nights": nights,
                "total": total,
            },
        },
    ]
    return _sse_response(speech, ui_events)


if __name__ == "__main__":
    app.run()
