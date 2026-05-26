# Copyright (c) Microsoft. All rights reserved.

"""Pipecat bot using SmallWebRTCTransport with the same multi-agent
architecture as ../pipecat-ws-server/bot_websocket_server.py.

  Main agent (no LLM/TTS):
      webrtc.in -> STT -> context_agg.user -> BusBridge ->
      webrtc.out -> context_agg.assistant

  LLM agents (with TTS):
      BusInput -> LLM -> TTS -> BusOutput

Two LLM agents:
  * "greeter"      - Zava online store greeter, voice en-US-Ava
  * "check_order"  - order-status agent, voice en-US-Andrew

The GreeterAgent / CheckOrderAgent classes and Azure helper services are
imported from the sibling pipecat-ws-server sample so the agent behavior
stays identical between the two transports.
"""

import os
import sys

# Make the sibling pipecat-ws-server directory importable. We reuse:
#   - GreeterAgent / CheckOrderAgent / ZavaTTSAgent
#   - _build_stt / _build_tts / _build_llm
#   - WallClockLatencyObserver
#   - AzureFastTranscriptionSTTService / AzureTTSTextStreamingService
_SIBLING = os.path.realpath(
    os.path.join(os.path.dirname(__file__), "..", "pipecat-ws-server")
)
if _SIBLING not in sys.path:
    sys.path.insert(0, _SIBLING)

from loguru import logger
from pipecat.audio.turn.smart_turn.local_smart_turn_v3 import LocalSmartTurnAnalyzerV3
from pipecat.audio.vad.silero import SileroVADAnalyzer
from pipecat.observers.turn_tracking_observer import TurnTrackingObserver
from pipecat.observers.user_bot_latency_observer import UserBotLatencyObserver
from pipecat.pipeline.pipeline import Pipeline
from pipecat.pipeline.task import PipelineParams, PipelineTask
from pipecat.processors.aggregators.llm_context import LLMContext
from pipecat.processors.aggregators.llm_response_universal import (
    LLMContextAggregatorPair,
    LLMUserAggregatorParams,
)
from pipecat.processors.frameworks.rtvi.frames import RTVIServerMessageFrame
from pipecat.transports.base_transport import TransportParams
from pipecat.transports.smallwebrtc.connection import SmallWebRTCConnection
from pipecat.transports.smallwebrtc.transport import SmallWebRTCTransport
from pipecat.turns.user_stop import TurnAnalyzerUserTurnStopStrategy
from pipecat.turns.user_turn_strategies import UserTurnStrategies

from pipecat_subagents.agents import BaseAgent, LLMAgentActivationArgs, agent_ready
from pipecat_subagents.bus import BusBridgeProcessor
from pipecat_subagents.runner import AgentRunner
from pipecat_subagents.types import AgentReadyData

# Reuse the LLM agents and helper services from the WebSocket sample.
from bot_websocket_server import (  # noqa: E402
    CheckOrderAgent,
    GreeterAgent,
    WallClockLatencyObserver,
    _build_stt,
)


# ---------------------------------------------------------------------------
# Main transport-owning agent (WebRTC version)
# ---------------------------------------------------------------------------


class ZavaMainAgent(BaseAgent):
    """Owns the SmallWebRTC transport and bridges frames to/from the bus."""

    def __init__(
        self, name: str, *, bus, transport: SmallWebRTCTransport
    ):
        super().__init__(name, bus=bus)
        self._transport = transport

    @agent_ready(name="greeter")
    async def on_greeter_ready(self, data: AgentReadyData) -> None:
        await self.activate_agent(
            "greeter",
            args=LLMAgentActivationArgs(
                messages=[
                    {
                        "role": "developer",
                        "content": (
                            "Welcome the user to the Zava online store, give a short "
                            "greeting, and ask whether they want to check an order or "
                            "report a product issue."
                        ),
                    },
                ],
            ),
        )

    def build_pipeline_task(self, pipeline: Pipeline) -> PipelineTask:
        turn_observer = TurnTrackingObserver(turn_end_timeout_secs=2.5)
        latency_observer = UserBotLatencyObserver()
        wall_clock_observer = WallClockLatencyObserver()

        task = PipelineTask(
            pipeline,
            enable_rtvi=True,
            idle_timeout_secs=None,
            params=PipelineParams(
                enable_metrics=True,
                enable_usage_metrics=True,
            ),
            observers=[turn_observer, latency_observer, wall_clock_observer],
        )

        @turn_observer.event_handler("on_turn_started")
        async def on_turn_started(observer, turn_count):
            logger.info(f"Turn {turn_count} started")
            await task.queue_frame(
                RTVIServerMessageFrame({"type": "turn-started", "turn_count": turn_count})
            )

        @turn_observer.event_handler("on_turn_ended")
        async def on_turn_ended(observer, turn_count, duration, was_interrupted):
            status = "interrupted" if was_interrupted else "completed"
            logger.info(f"Turn {turn_count} {status} after {duration:.2f}s")
            await task.queue_frame(
                RTVIServerMessageFrame(
                    {
                        "type": "turn-ended",
                        "turn_count": turn_count,
                        "duration": round(duration, 3),
                        "was_interrupted": was_interrupted,
                    }
                )
            )

        @latency_observer.event_handler("on_latency_measured")
        async def on_latency_measured(observer, latency_seconds):
            pass  # Suppressed in favor of wall-clock TTFA below.

        @wall_clock_observer.event_handler("on_wall_clock_latency")
        async def on_wall_clock_latency(observer, latency_seconds):
            logger.info(f"TTFA (time to first audio): {latency_seconds:.3f}s")
            await task.queue_frame(
                RTVIServerMessageFrame(
                    {
                        "type": "ttfa",
                        "latency_seconds": round(latency_seconds, 3),
                    }
                )
            )

        return task

    async def build_pipeline(self) -> Pipeline:
        stt = _build_stt()

        context = LLMContext()
        context_aggregator = LLMContextAggregatorPair(
            context,
            user_params=LLMUserAggregatorParams(
                vad_analyzer=SileroVADAnalyzer(),
                user_turn_strategies=UserTurnStrategies(
                    stop=[
                        TurnAnalyzerUserTurnStopStrategy(
                            turn_analyzer=LocalSmartTurnAnalyzerV3(),
                            enable_user_speaking_frames=False,
                        )
                    ],
                ),
            ),
        )

        bridge = BusBridgeProcessor(
            bus=self.bus,
            agent_name=self.name,
            name=f"{self.name}::BusBridge",
        )

        @self._transport.event_handler("on_client_connected")
        async def on_client_connected(transport, client):
            logger.info("WebRTC client connected")
            greeter = GreeterAgent("greeter", bus=self.bus)
            check_order = CheckOrderAgent("check_order", bus=self.bus)
            for agent in (greeter, check_order):
                await self.add_agent(agent)

        @self._transport.event_handler("on_client_disconnected")
        async def on_client_disconnected(transport, client):
            logger.info("WebRTC client disconnected")
            await self.cancel()

        return Pipeline(
            [
                self._transport.input(),
                stt,
                context_aggregator.user(),
                bridge,
                self._transport.output(),
                context_aggregator.assistant(),
            ]
        )


# ---------------------------------------------------------------------------
# Entry point used by server.py
# ---------------------------------------------------------------------------


async def run_bot(webrtc_connection: SmallWebRTCConnection) -> None:
    """Run the multi-agent bot bound to a SmallWebRTCConnection."""
    transport = SmallWebRTCTransport(
        webrtc_connection=webrtc_connection,
        params=TransportParams(
            audio_in_enabled=True,
            audio_out_enabled=True,
        ),
    )

    runner = AgentRunner(handle_sigint=False)
    main = ZavaMainAgent("zava", bus=runner.bus, transport=transport)
    await runner.add_agent(main)
    await runner.run()
