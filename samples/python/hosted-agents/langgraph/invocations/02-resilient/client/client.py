"""Launch the full-screen client for the durable Invocations sample."""

from __future__ import annotations

import argparse
import asyncio
from collections.abc import AsyncGenerator
from urllib.parse import urlsplit, urlunsplit
from uuid import uuid4

import httpx
from app import InvocationsCuiApp
from azure.identity.aio import DefaultAzureCredential
from conversation import Conversation

_AZURE_AI_SCOPE = "https://ai.azure.com/.default"


class _AzureBearerAuth(httpx.Auth):
    def __init__(self, credential: DefaultAzureCredential) -> None:
        self._credential = credential

    async def async_auth_flow(
        self, request: httpx.Request
    ) -> AsyncGenerator[httpx.Request, httpx.Response]:
        token = await self._credential.get_token(_AZURE_AI_SCOPE)
        request.headers["Authorization"] = f"Bearer {token.token}"
        yield request


def _invocations_url(url: str) -> str:
    parsed = urlsplit(url)
    path = parsed.path.rstrip("/")
    if not path.endswith("/invocations"):
        path = f"{path}/invocations"
    return urlunsplit(parsed._replace(path=path))


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Open the resilient LangGraph agent CUI."
    )
    parser.add_argument(
        "--url",
        default="http://127.0.0.1:8088",
        help="Agent host base URL or full protocol endpoint URL.",
    )
    parser.add_argument(
        "--auth",
        action="store_true",
        help="Add an Azure AI bearer token from DefaultAzureCredential.",
    )
    parser.add_argument(
        "--reconnect-timeout",
        type=float,
        default=120.0,
        help="Seconds to retry a disconnected request.",
    )
    return parser


async def amain(args: argparse.Namespace) -> None:
    credential: DefaultAzureCredential | None = None
    auth: httpx.Auth | None = None
    if args.auth:
        credential = DefaultAzureCredential()
        auth = _AzureBearerAuth(credential)

    try:
        async with httpx.AsyncClient(auth=auth, timeout=None) as client:
            conversation = Conversation(
                client,
                _invocations_url(args.url),
                session_id=str(uuid4()),
                reconnect_timeout=args.reconnect_timeout,
            )
            await InvocationsCuiApp(conversation).run_async()
    finally:
        if credential is not None:
            await credential.close()


def main() -> None:
    args = build_parser().parse_args()
    asyncio.run(amain(args))


if __name__ == "__main__":
    main()
