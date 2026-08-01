"""Advanced egress scenarios 8–12 for the deployed .NET agent."""

import pytest

from conftest import check_command, deployed_policy


CASES = [
    pytest.param(
        "e2e-dotnet-first-match",
        {
            "mode": "Enforced",
            "defaultAction": "Deny",
            "rules": [
                {
                    "name": "deny-ip",
                    "ruleType": "Fqdn",
                    "match": {"host": "httpbin.org", "path": "/ip"},
                    "action": {"actionType": "Deny"},
                },
                {
                    "name": "allow-httpbin",
                    "ruleType": "Fqdn",
                    "match": {"host": "httpbin.org"},
                    "action": {"actionType": "Allow"},
                },
            ],
        },
        [
            ("test egress to https://httpbin.org/get", ("200",), ()),
            ("test egress to https://httpbin.org/ip", ("403",), ()),
        ],
        id="08-first-match",
    ),
    pytest.param(
        "e2e-dotnet-multi-transform",
        {
            "mode": "Enforced",
            "defaultAction": "Deny",
            "rules": [
                {
                    "name": "multi-transform",
                    "ruleType": "Fqdn",
                    "match": {"host": "httpbin.org"},
                    "action": {
                        "actionType": "Transform",
                        "headers": [
                            {
                                "name": "X-Custom-Inserted",
                                "value": "hello",
                                "operation": "Insert",
                            },
                            {
                                "name": "User-Agent",
                                "value": "policy-agent/1.0",
                                "operation": "Set",
                            },
                            {"name": "X-Test-Marker", "operation": "Remove"},
                        ],
                    },
                }
            ],
        },
        [
            (
                "test headers to https://httpbin.org/headers",
                ("x-custom-inserted", "policy-agent"),
                ("x-test-marker",),
            )
        ],
        id="09-multi-transform",
    ),
    pytest.param(
        "e2e-dotnet-rewrite-transform",
        {
            "mode": "Enforced",
            "defaultAction": "Deny",
            "rules": [
                {
                    "name": "rewrite-to-bing",
                    "ruleType": "Fqdn",
                    "match": {"host": "www.google.com"},
                    "action": {
                        "actionType": "Rewrite",
                        "rewrite": {"scheme": "https", "host": "www.bing.com"},
                    },
                },
                {
                    "name": "tag-httpbin",
                    "ruleType": "Fqdn",
                    "match": {"host": "httpbin.org"},
                    "action": {
                        "actionType": "Transform",
                        "headers": [
                            {
                                "name": "X-Policy-Tag",
                                "value": "tagged",
                                "operation": "Insert",
                            }
                        ],
                    },
                },
            ],
        },
        [
            ("test egress to https://www.google.com", ("bing",), ()),
            (
                "test headers to https://httpbin.org/headers",
                ("x-policy-tag",),
                (),
            ),
            ("test egress to https://example.com", ("403",), ()),
        ],
        id="10-rewrite-transform",
    ),
    pytest.param(
        "e2e-dotnet-deny-all",
        {"mode": "Enforced", "defaultAction": "Deny", "rules": []},
        [("test connectivity", ("403",), ("| pass |",))],
        id="11-deny-all",
    ),
    pytest.param(
        "e2e-dotnet-wildcard",
        {
            "mode": "Enforced",
            "defaultAction": "Deny",
            "rules": [
                {
                    "name": "allow-org",
                    "ruleType": "Fqdn",
                    "match": {"host": "*.org"},
                    "action": {"actionType": "Allow"},
                }
            ],
        },
        [
            ("test egress to https://httpbin.org/get", ("200",), ()),
            ("test egress to https://example.com", ("403",), ()),
        ],
        id="12-wildcard",
    ),
]


@pytest.mark.parametrize("name,policy,checks", CASES)
def test_advanced_scenario(name, policy, checks):
    with deployed_policy(name, policy):
        for command, contains, excludes in checks:
            check_command(command, contains=contains, excludes=excludes)
