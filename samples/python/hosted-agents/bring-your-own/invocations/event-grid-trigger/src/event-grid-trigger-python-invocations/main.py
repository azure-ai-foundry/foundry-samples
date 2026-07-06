# Copyright (c) Microsoft. All rights reserved.

"""Event Grid → hosted agent (blob trigger), no APIM bridge.

A BYO Invocations agent that Event Grid POSTs to **directly**. EG
authenticates the delivery with the Event Grid system topic's
system-assigned managed identity whose AAD audience is set to
``https://ai.azure.com`` (the Foundry data plane), so the agent's
standard token validation accepts the request.

The handler accepts three shapes on POST /invocations:

* The Event Grid **subscription validation** handshake — answered with
  ``{"validationResponse": "<code>"}`` so the EG subscription can provision.
* An Event Grid batch of ``Microsoft.Storage.BlobCreated`` events — the
  container and blob name are extracted from ``data.url`` and the blob is
  processed.
* A direct ``{"container": "...", "name": "..."}`` payload — useful for
  quick local invokes via ``azd ai agent invoke``.

The blob is downloaded with the per-agent Microsoft Entra identity,
summarized with a Foundry model, and written back as
``<name>.summary.json`` to the container named by
``AZURE_STORAGE_SUMMARY_CONTAINER_NAME``. Using a sibling output container
avoids re-triggering the Event Grid subscription that watches the input
container.

Required environment variables:

    FOUNDRY_PROJECT_ENDPOINT             (auto-injected in hosted containers)
    AZURE_AI_MODEL_DEPLOYMENT_NAME
    AZURE_STORAGE_ACCOUNT_NAME
    AZURE_STORAGE_SUMMARY_CONTAINER_NAME
"""

import json
import logging
import os
import time
from urllib.parse import urlparse

from dotenv import load_dotenv
from starlette.requests import Request
from starlette.responses import JSONResponse

from azure.ai.projects.aio import AIProjectClient
from azure.identity.aio import DefaultAzureCredential
from azure.storage.blob.aio import BlobClient

from azure.ai.agentserver.invocations import InvocationAgentServerHost

load_dotenv()

logger = logging.getLogger(__name__)
logging.basicConfig(level=logging.INFO)


_endpoint = os.environ["FOUNDRY_PROJECT_ENDPOINT"]
_model = os.environ["AZURE_AI_MODEL_DEPLOYMENT_NAME"]
_storage_account = os.environ.get("AZURE_STORAGE_ACCOUNT_NAME", "")
_summary_container = os.environ.get("AZURE_STORAGE_SUMMARY_CONTAINER_NAME", "")

_credential = DefaultAzureCredential()
_project_client = AIProjectClient(endpoint=_endpoint, credential=_credential)
_openai_client = _project_client.get_openai_client()

_MAX_BLOB_BYTES = 64 * 1024
_ALLOWED_EXTENSIONS = (".txt", ".md")

_EG_SUBSCRIPTION_VALIDATION_EVENT_TYPE = (
    "Microsoft.EventGrid.SubscriptionValidationEvent"
)
_EG_BLOB_CREATED_EVENT_TYPE = "Microsoft.Storage.BlobCreated"


def _iter_eg_events(payload):
    """Yield event dicts from an EG (array) or CloudEvents (single) payload."""

    if isinstance(payload, list):
        for event in payload:
            if isinstance(event, dict):
                yield event
    elif isinstance(payload, dict):
        # CloudEvents v1.0 delivers a single event object. EG schema is
        # always an array, but some testers post a single-event dict.
        if payload.get("eventType") or payload.get("type"):
            yield payload


def _extract_subscription_validation_event(payload):
    """Return the SubscriptionValidationEvent object, or ``None``."""

    for event in _iter_eg_events(payload):
        event_type = event.get("eventType") or event.get("type")
        if event_type == _EG_SUBSCRIPTION_VALIDATION_EVENT_TYPE:
            return event
    return None


def _extract_blob_created(payload):
    """Return (container, name) from the first BlobCreated event, or ``None``.

    Both EG schema (``data.url``) and CloudEvents schema (``data.url``)
    expose the full blob URL — split the path into ``<container>/<name>``.
    """

    for event in _iter_eg_events(payload):
        event_type = event.get("eventType") or event.get("type")
        if event_type != _EG_BLOB_CREATED_EVENT_TYPE:
            continue
        url = (event.get("data") or {}).get("url")
        if not url:
            continue
        path = urlparse(url).path.lstrip("/")
        parts = path.split("/", 1)
        if len(parts) != 2 or not parts[0] or not parts[1]:
            continue
        return parts[0], parts[1]
    return None


app = InvocationAgentServerHost()


@app.invoke_handler
async def handle_invoke(request: Request):
    """POST /invocations — EG envelope or ``{"container": "...", "name": "..."}``."""

    body = await request.body()
    try:
        payload = json.loads(body) if body else {}
    except json.JSONDecodeError as exc:
        return JSONResponse(
            status_code=400, content={"error": "invalid_json", "message": str(exc)}
        )

    # 1. Event Grid subscription validation handshake. EG sends this once
    #    when the subscription is created; the agent must echo the code back
    #    or the subscription fails to provision.
    #    https://learn.microsoft.com/azure/event-grid/troubleshoot-subscription-validation
    validation_event = _extract_subscription_validation_event(payload)
    if validation_event is not None:
        validation_code = (validation_event.get("data") or {}).get("validationCode")
        if not validation_code:
            return JSONResponse(
                status_code=400,
                content={
                    "error": "invalid_validation_event",
                    "message": "missing data.validationCode",
                },
            )
        logger.info(
            "event-grid-trigger:subscription-validation code=%s", validation_code
        )
        return JSONResponse({"validationResponse": validation_code})

    # 2. Event Grid BlobCreated batch: extract (container, name) from data.url.
    blob_ref = _extract_blob_created(payload)
    if blob_ref is not None:
        container, blob_name = blob_ref
    else:
        # 3. Direct {container, name} for ``azd ai agent invoke`` testing.
        if not isinstance(payload, dict):
            return JSONResponse(
                status_code=400,
                content={
                    "error": "invalid_payload",
                    "message": "expected an EG event batch or {container, name}",
                },
            )
        container = payload.get("container")
        blob_name = payload.get("name")
    if not container or not blob_name:
        if isinstance(payload, dict) and (
            payload.get("query") or payload.get("message") or payload.get("input")
        ):
            return JSONResponse(
                {
                    "info": (
                        "This agent is triggered by Azure Storage BlobCreated events "
                        "via Event Grid. Send {\"container\": \"...\", \"name\": "
                        "\"...\"} (or upload a blob to the watched container) to run "
                        "a real turn."
                    ),
                }
            )
        return JSONResponse(
            status_code=400,
            content={"error": "invalid_payload", "message": "expected {container, name}"},
        )

    if not blob_name.lower().endswith(_ALLOWED_EXTENSIONS):
        return JSONResponse(
            {"skipped": True, "reason": f"extension not in {_ALLOWED_EXTENSIONS}"}
        )

    if not _storage_account or not _summary_container:
        return JSONResponse(
            status_code=500,
            content={
                "error": "missing_configuration",
                "message": "AZURE_STORAGE_ACCOUNT_NAME and AZURE_STORAGE_SUMMARY_CONTAINER_NAME must be set",
            },
        )

    account_url = f"https://{_storage_account}.blob.core.windows.net"

    async with BlobClient(account_url, container, blob_name, credential=_credential) as src:
        downloader = await src.download_blob(max_concurrency=1)
        raw = await downloader.readall()
    truncated = raw[:_MAX_BLOB_BYTES]
    text = truncated.decode("utf-8", errors="replace")

    started = time.monotonic()
    response = await _openai_client.responses.create(
        model=_model,
        instructions="You summarize files in 3-5 concise bullet points.",
        input=[
            {
                "role": "user",
                "content": (
                    f"Summarize the following file ({blob_name}) in 3-5 bullet points:\n\n"
                    f"{text}"
                ),
            }
        ],
        store=False,
    )
    summary = response.output_text or ""
    elapsed_ms = int((time.monotonic() - started) * 1000)

    # Persist the summary to a sibling container so the result is visible in
    # Storage Explorer / the portal. The output container is intentionally
    # different from the input so writes do not re-trigger Event Grid.
    summary_blob_name = f"{blob_name}.summary.json"
    summary_doc = {
        "input": f"{container}/{blob_name}",
        "elapsed_ms": elapsed_ms,
        "truncated": len(raw) > _MAX_BLOB_BYTES,
        "summary": summary,
    }
    async with BlobClient(
        account_url, _summary_container, summary_blob_name, credential=_credential
    ) as dst:
        await dst.upload_blob(
            json.dumps(summary_doc, ensure_ascii=False, indent=2).encode("utf-8"),
            overwrite=True,
            content_type="application/json",
        )

    logger.info(
        "event-grid-trigger:summary blob=%s/%s elapsed_ms=%d truncated=%s output=%s/%s",
        container,
        blob_name,
        elapsed_ms,
        len(raw) > _MAX_BLOB_BYTES,
        _summary_container,
        summary_blob_name,
    )

    return JSONResponse(
        {
            "input": f"{container}/{blob_name}",
            "output": f"{_summary_container}/{summary_blob_name}",
            "elapsed_ms": elapsed_ms,
            "truncated": len(raw) > _MAX_BLOB_BYTES,
            "summary": summary,
        }
    )


if __name__ == "__main__":
    app.run()
