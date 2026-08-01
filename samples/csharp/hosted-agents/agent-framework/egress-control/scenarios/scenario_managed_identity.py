"""Managed-identity egress scenarios 16–17 for the deployed .NET agent."""

import os

import pytest

from conftest import (
    AGENT_STORAGE_ACCOUNT,
    AGENT_STORAGE_CONTAINER,
    check_command,
    deployed_policy,
)


pytestmark = pytest.mark.skipif(
    os.getenv("EGRESS_MI_ENABLED", "").lower() not in ("1", "true", "yes", "on"),
    reason=(
        "managedIdentityRef token injection is not yet functional; "
        "set EGRESS_MI_ENABLED=1 when the platform capability is available"
    ),
)


def managed_identity_policy() -> dict:
    if not AGENT_STORAGE_ACCOUNT:
        pytest.skip("AGENT_STORAGE_ACCOUNT is required")

    storage_host = f"{AGENT_STORAGE_ACCOUNT}.blob.core.windows.net"
    return {
        "mode": "Enforced",
        "defaultAction": "Deny",
        "rules": [
            {
                "name": "mi-storage-token",
                "ruleType": "Fqdn",
                "match": {"host": storage_host},
                "action": {
                    "actionType": "Transform",
                    "headers": [
                        {
                            "name": "Authorization",
                            "operation": "Set",
                            "valueRef": {
                                "managedIdentityRef": {
                                    "resource": (
                                        "https://storage.azure.com/.default"
                                    ),
                                    "format": "Bearer {token}",
                                }
                            },
                        },
                        {
                            "name": "x-ms-version",
                            "value": "2023-11-03",
                            "operation": "Set",
                        },
                    ],
                },
            },
            {
                "name": "allow-httpbin",
                "ruleType": "Fqdn",
                "match": {"host": "httpbin.org"},
                "action": {"actionType": "Allow"},
            },
        ],
    }


def test_16_managed_identity_storage_access():
    storage_url = (
        f"https://{AGENT_STORAGE_ACCOUNT}.blob.core.windows.net"
        f"/{AGENT_STORAGE_CONTAINER}?restype=container&comp=list"
    )
    with deployed_policy("e2e-dotnet-mi-storage", managed_identity_policy()):
        check_command(
            f"test egress to {storage_url}",
            contains=("200",),
            excludes=("authorizationfailure", "401"),
        )


def test_17_managed_identity_host_scoping():
    with deployed_policy("e2e-dotnet-mi-scoping", managed_identity_policy()):
        check_command(
            "test headers to https://httpbin.org/headers",
            contains=("200",),
            excludes=("authorization", "bearer"),
        )
        check_command(
            "test egress to https://httpbin.org/get",
            contains=("200",),
        )
