"""Launch the full-screen client for the resilient Responses sample."""

from __future__ import annotations

import argparse
import asyncio
from urllib.parse import parse_qsl, urlsplit, urlunsplit
from uuid import uuid4

from app import ResponsesCuiApp
from azure.identity import DefaultAzureCredential, get_bearer_token_provider
from conversation import Conversation
from openai import AsyncOpenAI

_AZURE_AI_SCOPE = "https://ai.azure.com/.default"
_FOUNDRY_RESPONSES_API_VERSION = "v1"


def _openai_base_url(responses_url: str) -> str:
    parsed = urlsplit(responses_url)
    path = parsed.path.rstrip("/").removesuffix("/responses")
    return urlunsplit((parsed.scheme, parsed.netloc, path, "", ""))


def _openai_default_query(
    responses_url: str,
    *,
    authenticated: bool,
) -> dict[str, str]:
    query = dict(parse_qsl(urlsplit(responses_url).query, keep_blank_values=True))
    if authenticated:
        query.setdefault("api-version", _FOUNDRY_RESPONSES_API_VERSION)
    return query


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
    if args.auth:
        token_provider = get_bearer_token_provider(
            DefaultAzureCredential(), _AZURE_AI_SCOPE
        )

        async def api_key() -> str:
            return await asyncio.to_thread(token_provider)
    else:
        api_key = "local"

    async with AsyncOpenAI(
        base_url=_openai_base_url(args.url),
        api_key=api_key,
        default_query=_openai_default_query(args.url, authenticated=args.auth),
        max_retries=0,
    ) as client:
        conversation = Conversation(
            client,
            reconnect_timeout=args.reconnect_timeout,
            conversation_id=None if args.auth else str(uuid4()),
        )
        await ResponsesCuiApp(conversation).run_async()


def main() -> None:
    args = build_parser().parse_args()
    asyncio.run(amain(args))


if __name__ == "__main__":
    main()
