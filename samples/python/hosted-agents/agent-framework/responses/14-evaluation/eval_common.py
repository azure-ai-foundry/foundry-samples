# Copyright (c) Microsoft. All rights reserved.

"""Shared helpers for the ``evaluate_*.py`` and ``generate_dataset_*.py``
scripts in this sample.

These helpers wrap two patterns:

* The typed Foundry / OpenAI client surfaces — ``AIProjectClient`` and
  ``openai_client.evals.{create,runs.create,runs.retrieve}``. Eval groups and
  eval runs are GA-ish in ``2025-11-15-preview`` and exposed via
  ``openai_client.evals``.

* Raw REST against preview LROs (evaluator generation, data generation,
  scheduled eval). The typed Python surface for these may not be available
  yet; until it lands, the scripts call the endpoints directly with an AAD
  bearer token from ``DefaultAzureCredential``. Bump ``API_VERSION`` here
  when the GA version ships.
"""

from __future__ import annotations

import os
import time
from typing import Any, Mapping
from urllib.parse import urlparse

import requests
from azure.identity import DefaultAzureCredential

#: API version pinned for all raw-REST calls in this sample. Matches the
#: version used by the Foundry evaluations bug-bash workspace
#: (``foundry-observability-playground/bugbash``) at time of writing.
#: Override per-script by passing ``api_version=`` to the helpers below.
API_VERSION = "2025-11-15-preview"

#: AAD scope used to mint a bearer token for the raw-REST surface.
AI_SCOPE = "https://ai.azure.com/.default"


def project_endpoint() -> str:
    """Read ``FOUNDRY_PROJECT_ENDPOINT`` from the environment.

    Form: ``https://<account>.services.ai.azure.com/api/projects/<project>``.
    """
    endpoint = os.environ.get("FOUNDRY_PROJECT_ENDPOINT")
    if not endpoint:
        raise RuntimeError(
            "FOUNDRY_PROJECT_ENDPOINT is not set. See .env.example."
        )
    return endpoint.rstrip("/")


def model_deployment_name(default: str = "gpt-4.1-mini") -> str:
    return os.environ.get("AZURE_AI_MODEL_DEPLOYMENT_NAME", default)


def target_agent() -> dict[str, str]:
    """Identify the deployed hosted agent that eval runs should target.

    Returns the ``target.azure_ai_agent`` payload sub-object used by
    ``azure_ai_target_completions`` data sources.
    """
    name = os.environ.get("EVAL_AGENT_NAME", "agent-framework-agent-evaluation-responses")
    version = os.environ.get("EVAL_AGENT_VERSION", "1")
    return {"type": "azure_ai_agent", "name": name, "version": version}


def _aad_token(credential: DefaultAzureCredential | None = None) -> str:
    credential = credential or DefaultAzureCredential()
    return credential.get_token(AI_SCOPE).token


def rest_headers(credential: DefaultAzureCredential | None = None) -> dict[str, str]:
    """Authorization + content headers for raw REST calls."""
    return {
        "Authorization": f"Bearer {_aad_token(credential)}",
        "Content-Type": "application/json",
    }


def rest_url(path: str, api_version: str = API_VERSION) -> str:
    """Build ``<project_endpoint><path>?api-version=<api_version>``."""
    base = project_endpoint()
    sep = "&" if "?" in path else "?"
    return f"{base}{path}{sep}api-version={api_version}"


def _check(response: requests.Response) -> Any:
    if not response.ok:
        snippet = response.text[:1000].replace("\n", " ")
        raise RuntimeError(
            f"{response.request.method} {urlparse(response.url).path} "
            f"failed with {response.status_code}: {snippet}"
        )
    return response.json() if response.text else None


def rest_post(path: str, body: Mapping[str, Any], *, api_version: str = API_VERSION) -> Any:
    return _check(
        requests.post(rest_url(path, api_version), headers=rest_headers(), json=body, timeout=120)
    )


def rest_get(path: str, *, api_version: str = API_VERSION) -> Any:
    return _check(
        requests.get(rest_url(path, api_version), headers=rest_headers(), timeout=120)
    )


def rest_patch(path: str, body: Mapping[str, Any], *, api_version: str = API_VERSION) -> Any:
    headers = rest_headers()
    headers["Content-Type"] = "application/merge-patch+json"
    return _check(
        requests.patch(rest_url(path, api_version), headers=headers, json=body, timeout=120)
    )


def rest_delete(path: str, *, api_version: str = API_VERSION) -> None:
    response = requests.delete(rest_url(path, api_version), headers=rest_headers(), timeout=120)
    if not response.ok and response.status_code != 404:
        raise RuntimeError(
            f"DELETE {urlparse(response.url).path} failed: {response.status_code}: {response.text[:500]}"
        )


def poll_lro(
    poll_fn,
    *,
    terminal_states: tuple[str, ...] = ("succeeded", "failed", "canceled", "completed"),
    interval_seconds: float = 5.0,
    max_seconds: float = 600.0,
    description: str = "job",
):
    """Poll an LRO until it reaches a terminal state.

    ``poll_fn`` must be a no-arg callable returning the latest job dict (must
    include a ``"status"`` field). Prints progress to stdout.
    """
    start = time.monotonic()
    last_status: str | None = None
    while True:
        job = poll_fn()
        status = job.get("status")
        if status != last_status:
            print(f"  [{description}] status: {status}")
            last_status = status
        if status in terminal_states:
            return job
        if time.monotonic() - start > max_seconds:
            raise TimeoutError(
                f"{description} did not reach a terminal state within {max_seconds:.0f}s "
                f"(last status: {status})"
            )
        time.sleep(interval_seconds)


def _as_dict(obj: Any) -> Any:
    """Best-effort: convert OpenAI/Pydantic objects to plain dicts.

    Many ``openai.evals`` types are Pydantic models — they support
    attribute access (``item.sample.output_text``) but not ``.get()``.
    The friendly formatters below operate on plain dicts/lists, so we
    normalize once here.
    """
    if obj is None or isinstance(obj, (str, int, float, bool)):
        return obj
    if isinstance(obj, Mapping):
        return {k: _as_dict(v) for k, v in obj.items()}
    if isinstance(obj, (list, tuple)):
        return [_as_dict(v) for v in obj]
    for attr in ("model_dump", "to_dict", "dict"):
        fn = getattr(obj, attr, None)
        if callable(fn):
            try:
                return _as_dict(fn())
            except TypeError:
                continue
    if hasattr(obj, "__dict__"):
        return {k: _as_dict(v) for k, v in vars(obj).items() if not k.startswith("_")}
    return obj


def _truncate(value: Any, limit: int = 240) -> str:
    """Return ``value`` as a single-line string trimmed to ``limit`` chars."""
    text = str(value).replace("\n", " ").strip()
    return text if len(text) <= limit else text[: limit - 1] + "…"


def _extract_query(item: Any) -> str:
    """Best-effort: pull the user-facing question out of an output item."""
    if not item:
        return ""
    ds = item.get("datasource_item") or {}
    if isinstance(ds, Mapping):
        for key in ("query", "question", "prompt", "input"):
            if ds.get(key):
                return _truncate(ds[key])
        msgs = ds.get("messages")
        if isinstance(msgs, list) and msgs:
            first_user = next(
                (m for m in msgs if (m or {}).get("role") == "user"),
                None,
            )
            if first_user:
                content = first_user.get("content", "")
                # Conversation-shape: print a brief multi-turn summary.
                count = len([m for m in msgs if m])
                return _truncate(f"[{count}-turn conversation] first user: {content}")
    return ""


def _stringify_content(content: Any) -> str:
    """Flatten an OpenAI-style ``content`` field to a single string."""
    if isinstance(content, str):
        return content
    if isinstance(content, list):
        bits: list[str] = []
        for piece in content:
            if isinstance(piece, Mapping):
                text = piece.get("text") or piece.get("output_text") or piece.get("content")
                if text:
                    bits.append(_stringify_content(text))
            elif isinstance(piece, str):
                bits.append(piece)
        return " ".join(bits)
    return str(content) if content is not None else ""


def _extract_response(item: Any) -> str:
    """Best-effort: pull the agent's answer out of an output item."""
    if not item:
        return ""
    sample = item.get("sample") if isinstance(item, Mapping) else None
    if isinstance(sample, Mapping):
        if sample.get("output_text"):
            return _truncate(sample["output_text"])
        out = sample.get("output")
        if out:
            text = _stringify_content(out)
            if text:
                return _truncate(text)
    if isinstance(item, Mapping):
        if item.get("output_text"):
            return _truncate(item["output_text"])
        ds = item.get("datasource_item") or {}
        if isinstance(ds, Mapping) and ds.get("response"):
            return _truncate(ds["response"])
    return ""


def _format_score_block(results: Any) -> list[str]:
    """Format the per-evaluator scores attached to one output item."""
    lines: list[str] = []
    if not results:
        return lines
    for r in results or []:
        if not isinstance(r, Mapping):
            continue
        name = r.get("name") or r.get("type") or "evaluator"
        passed = r.get("passed")
        score = r.get("score")
        verdict = "PASS" if passed else ("FAIL" if passed is False else "—")
        score_str = f"{score}" if score is not None else "n/a"
        lines.append(f"    {name:24s}  score={score_str:5s}  {verdict}")
        reason = r.get("reason") or r.get("reasoning")
        if not reason:
            sample = r.get("sample")
            if isinstance(sample, Mapping):
                reason = sample.get("reason") or sample.get("reasoning")
        if reason:
            lines.append(f"        rationale: {_truncate(reason, 200)}")
    return lines


def print_friendly_output(output_items: list, limit: int = 3) -> None:
    """Print the first ``limit`` output items in a beginner-friendly shape.

    Each item shows the user question, the agent's answer (trimmed), and
    every evaluator's score on its own line, plus a one-line rationale if
    the service returned one. Set ``EVAL_DEBUG=1`` to also print the raw
    item via ``pprint`` for the first row — handy when the friendly shape
    is missing fields you care about.
    """
    if not output_items:
        print("(no output items returned)")
        return

    debug = os.environ.get("EVAL_DEBUG", "").lower() in {"1", "true", "yes"}

    shown = min(limit, len(output_items))
    print(f"\nShowing {shown} of {len(output_items)} output items:")
    print("(set EVAL_DEBUG=1 to also see the raw payload.)\n")
    for idx, item in enumerate(output_items[:shown], start=1):
        normalized = _as_dict(item)
        query = _extract_query(normalized) or "(not extracted — set EVAL_DEBUG=1)"
        response = _extract_response(normalized)

        print(f"  [{idx}] Question: {query}")
        if response:
            print(f"      Answer:   {response}")

        results = normalized.get("results") if isinstance(normalized, Mapping) else None
        for line in _format_score_block(results):
            print(line)
        print()

    if debug:
        from pprint import pprint as _pprint
        print("--- EVAL_DEBUG: raw first output item ---")
        _pprint(output_items[0])
        print("--- end raw item ---\n")
