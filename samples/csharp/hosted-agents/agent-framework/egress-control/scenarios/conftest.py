"""Shared Azure lifecycle helpers for the .NET egress-control scenarios."""

from __future__ import annotations

import json
import os
import subprocess
import time
import urllib.error
import urllib.request
from contextlib import contextmanager

import pytest


FOUNDRY_ENDPOINT = os.getenv("FOUNDRY_ENDPOINT", "")
AGENT_NAME = os.getenv("AGENT_NAME", "")
COGSVC_ACCOUNT_ID = os.getenv("COGSVC_ACCOUNT_ID", "")
AGENT_IMAGE = os.getenv("AGENT_IMAGE", "")
MODEL_DEPLOYMENT = os.getenv("MODEL_DEPLOYMENT", "gpt-4.1")
RAI_BASE_POLICY = os.getenv("RAI_BASE_POLICY", "Microsoft.DefaultV2")
INVOKE_DELAY = int(os.getenv("INVOKE_DELAY", "15"))
AGENT_STORAGE_ACCOUNT = os.getenv("AGENT_STORAGE_ACCOUNT", "")
AGENT_STORAGE_CONTAINER = os.getenv("AGENT_STORAGE_CONTAINER", "egress-test")
# Hosted-agent version management and Responses invocation use this data-plane
# API version; RAI policy management is exposed by the newer ARM API below.
API_VERSION = "2025-05-15-preview"
POLICY_API_VERSION = "2026-05-15-preview"

_rai_base_cache: dict | None = None


def get_token(resource: str) -> str:
    return subprocess.check_output(
        [
            "az",
            "account",
            "get-access-token",
            "--resource",
            resource,
            "--query",
            "accessToken",
            "-o",
            "tsv",
        ],
        text=True,
        stderr=subprocess.DEVNULL,
    ).strip()


def request_json(
    method: str,
    url: str,
    token: str,
    data: dict | None = None,
) -> dict:
    body = json.dumps(data).encode() if data is not None else None
    request = urllib.request.Request(
        url,
        data=body,
        method=method,
        headers={
            "Authorization": "Bearer " + token,
            "Content-Type": "application/json",
        },
    )
    try:
        with urllib.request.urlopen(request, timeout=120) as response:
            payload = response.read()
    except urllib.error.HTTPError as error:
        payload = error.read()
        if not payload:
            return {"error": {"message": f"HTTP {error.code}: {error.reason}"}}

    return json.loads(payload) if payload.strip() else {}


def get_rai_base_config() -> dict:
    global _rai_base_cache
    if _rai_base_cache is not None:
        return _rai_base_cache

    token = get_token("https://management.azure.com")
    url = (
        f"https://management.azure.com{COGSVC_ACCOUNT_ID}"
        f"/raiPolicies/{RAI_BASE_POLICY}?api-version={POLICY_API_VERSION}"
    )
    response = request_json("GET", url, token)
    properties = response.get("properties", {})
    _rai_base_cache = {
        "basePolicyName": properties.get("basePolicyName", RAI_BASE_POLICY),
        "contentFilters": properties.get("contentFilters", []),
    }
    return _rai_base_cache


def create_egress_policy(name: str, egress_policy: dict) -> None:
    token = get_token("https://management.azure.com")
    body = {
        "properties": {
            **get_rai_base_config(),
            "egressPolicy": egress_policy,
        }
    }
    url = (
        f"https://management.azure.com{COGSVC_ACCOUNT_ID}"
        f"/raiPolicies/{name}?api-version={POLICY_API_VERSION}"
    )
    response = request_json("PUT", url, token, body)
    if response.get("error"):
        pytest.fail(f"Policy creation failed: {response['error']}")


def delete_egress_policy(name: str) -> None:
    token = get_token("https://management.azure.com")
    url = (
        f"https://management.azure.com{COGSVC_ACCOUNT_ID}"
        f"/raiPolicies/{name}?api-version={POLICY_API_VERSION}"
    )
    response = request_json("DELETE", url, token)
    if response.get("error"):
        print(f"Policy cleanup failed for {name}: {response['error']}")


def deploy_agent_version(policy_name: str) -> str:
    token = get_token("https://ai.azure.com")
    policy_id = f"{COGSVC_ACCOUNT_ID}/raiPolicies/{policy_name}"
    response = request_json(
        "POST",
        f"{FOUNDRY_ENDPOINT}/agents/{AGENT_NAME}/versions?api-version={API_VERSION}",
        token,
        {
            "definition": {
                "kind": "hosted",
                "image": AGENT_IMAGE,
                "container_protocol_versions": [
                    {"protocol": "responses", "version": "2.0.0"}
                ],
                "cpu": "0.5",
                "memory": "1Gi",
                "environment_variables": {
                    "AZURE_AI_MODEL_DEPLOYMENT_NAME": MODEL_DEPLOYMENT,
                    "EGRESS_TEST_VERIFY_TLS": os.getenv(
                        "EGRESS_TEST_VERIFY_TLS", "true"
                    ),
                },
                "rai_config": {"rai_policy_name": policy_id},
            }
        },
    )
    version = response.get("version")
    if version is None:
        pytest.fail(f"Version creation failed: {response.get('error', response)}")

    pin = request_json(
        "PATCH",
        f"{FOUNDRY_ENDPOINT}/agents/{AGENT_NAME}?api-version={API_VERSION}",
        token,
        {
            "agent_endpoint": {
                "version_selector": {
                    "version_selection_rules": [
                        {
                            "type": "FixedRatio",
                            "agent_version": str(version),
                            "traffic_percentage": 100,
                        }
                    ]
                }
            }
        },
    )
    if pin.get("error"):
        pytest.fail(f"Traffic pinning failed: {pin['error']}")

    for _ in range(20):
        status = request_json(
            "GET",
            (
                f"{FOUNDRY_ENDPOINT}/agents/{AGENT_NAME}/versions/{version}"
                f"?api-version={API_VERSION}"
            ),
            token,
        )
        if status.get("status") == "active":
            return str(version)
        time.sleep(5)

    pytest.fail(
        f"Version {version} not active after 100s "
        f"(status={status.get('status', '?')})"
    )


def invoke_agent(command: str, delay: int | None = None) -> str:
    token = get_token("https://ai.azure.com")
    url = (
        f"{FOUNDRY_ENDPOINT}/agents/{AGENT_NAME}"
        f"/endpoint/protocols/openai/responses?api-version={API_VERSION}"
    )
    response = request_json("POST", url, token, {"input": command})
    if response.get("error"):
        error = response["error"]
        message = str(error.get("message", error))
        if "429" in message or "rate_limit" in message.lower():
            time.sleep(30)
            token = get_token("https://ai.azure.com")
            response = request_json("POST", url, token, {"input": command})
        if response.get("error"):
            return f"ERROR: {response['error']}"

    text = "".join(
        content["text"]
        for item in response.get("output", [])
        if item.get("type") == "message"
        for content in item.get("content", [])
        if content.get("type") == "output_text"
    )
    time.sleep(INVOKE_DELAY if delay is None else delay)
    return text


@contextmanager
def deployed_policy(name: str, policy: dict):
    create_egress_policy(name, policy)
    try:
        deploy_agent_version(name)
        time.sleep(20)
        yield
    finally:
        delete_egress_policy(name)


def check_command(
    command: str,
    *,
    contains: tuple[str, ...] = (),
    contains_any: tuple[str, ...] = (),
    excludes: tuple[str, ...] = (),
) -> str:
    result = invoke_agent(command)
    if "503" in result:
        time.sleep(15)
        result = invoke_agent(command)

    if result.startswith("ERROR:"):
        pytest.fail(f"Agent invocation failed before egress evaluation: {result[:400]}")

    lower = result.lower()
    for value in set(contains):
        required_count = contains.count(value)
        actual_count = lower.count(value.lower())
        assert actual_count >= required_count, (
            f"Expected {value!r} at least {required_count} time(s) "
            f"for {command!r}: {result[:400]}"
        )
    if contains_any:
        assert any(value.lower() in lower for value in contains_any), (
            f"Expected one of {contains_any!r} for {command!r}: {result[:400]}"
        )
    for value in excludes:
        assert value.lower() not in lower, (
            f"Did not expect {value!r} for {command!r}: {result[:400]}"
        )
    return result


@pytest.fixture(autouse=True, scope="session")
def check_environment():
    required = {
        "FOUNDRY_ENDPOINT": FOUNDRY_ENDPOINT,
        "AGENT_NAME": AGENT_NAME,
        "COGSVC_ACCOUNT_ID": COGSVC_ACCOUNT_ID,
        "AGENT_IMAGE": AGENT_IMAGE,
    }
    missing = [name for name, value in required.items() if not value]
    if missing:
        pytest.skip(f"Missing required env vars: {', '.join(missing)}")
