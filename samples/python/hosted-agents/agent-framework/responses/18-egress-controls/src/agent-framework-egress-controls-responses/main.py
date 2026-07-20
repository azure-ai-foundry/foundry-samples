# Copyright (c) Microsoft. All rights reserved.

import os
import socket
import ssl
import time
import urllib.error
import urllib.parse
import urllib.request

from agent_framework import Agent, tool
from agent_framework.foundry import FoundryChatClient
from agent_framework_foundry_hosting import ResponsesHostServer
from azure.identity import DefaultAzureCredential
from dotenv import load_dotenv
from pydantic import Field
from typing_extensions import Annotated

load_dotenv()


@tool(approval_mode="never_require")
def probe_egress(
    url: Annotated[str, Field(description="Full HTTPS URL to GET, for example https://example.com")],
) -> str:
    """Perform an outbound HTTPS GET and return the exact result."""
    started = time.time()
    out = [f"PROBE url={url}"]

    parsed = urllib.parse.urlparse(url)
    if parsed.scheme != "https" or not parsed.hostname:
        return " | ".join(out + ["INPUT_ERROR url must be an absolute HTTPS URL"])

    try:
        ip = socket.gethostbyname(parsed.hostname)
        out.append(f"DNS host={parsed.hostname} -> {ip}")
    except Exception as e:  # noqa: BLE001
        out.append(f"DNS_ERROR {type(e).__name__}: {e}")

    try:
        req = urllib.request.Request(url, headers={"User-Agent": "foundry-egress-controls-sample/1.0"})
        with urllib.request.urlopen(req, timeout=15) as resp:
            body = resp.read(500).decode("utf-8", "replace")
            out.append(f"HTTP_OK status={resp.status} bytes={len(body)} body={body!r}")
    except urllib.error.HTTPError as e:
        body = e.read(500).decode("utf-8", "replace")
        out.append(f"HTTP_ERROR status={e.code} reason={e.reason} body={body!r}")
    except urllib.error.URLError as e:
        out.append(f"URL_ERROR reason={e.reason!r} cause={type(e.reason).__name__}")
    except (ssl.SSLError, socket.timeout) as e:
        out.append(f"NET_ERROR {type(e).__name__}: {e}")
    except Exception as e:  # noqa: BLE001
        out.append(f"OTHER_ERROR {type(e).__name__}: {e}")

    out.append(f"elapsed_ms={int((time.time() - started) * 1000)}")
    result = " | ".join(out)
    print(f"[egress-controls] {result}", flush=True)
    return result


def main():
    client = FoundryChatClient(
        project_endpoint=os.environ["FOUNDRY_PROJECT_ENDPOINT"],
        model=os.environ["AZURE_AI_MODEL_DEPLOYMENT_NAME"],
        credential=DefaultAzureCredential(),
    )

    agent = Agent(
        client=client,
        instructions=(
            "You are a network egress test harness. When the user asks you to test, "
            "probe, fetch, allow, or block a URL, call probe_egress with the exact "
            "HTTPS URL and return the raw tool output verbatim."
        ),
        tools=[probe_egress],
        default_options={"store": False},
    )

    server = ResponsesHostServer(agent)
    server.run()


if __name__ == "__main__":
    main()
