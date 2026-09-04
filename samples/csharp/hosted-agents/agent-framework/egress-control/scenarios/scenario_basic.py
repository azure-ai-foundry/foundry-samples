"""Basic egress scenarios 1–7 for the deployed .NET agent."""

import pytest

from conftest import check_command, deployed_policy


CASES = [
    pytest.param(
        "e2e-dotnet-allow-deny",
        {
            "mode": "Enforced",
            "defaultAction": "Deny",
            "rules": [
                {
                    "name": "allow-httpbin",
                    "ruleType": "Fqdn",
                    "match": {"host": "httpbin.org"},
                    "action": {"actionType": "Allow"},
                }
            ],
        },
        [
            ("test egress to https://httpbin.org/get", ("200",), ()),
            ("test egress to https://example.com", ("403",), ()),
        ],
        id="01-allow-deny",
    ),
    pytest.param(
        "e2e-dotnet-transform-insert",
        {
            "mode": "Enforced",
            "defaultAction": "Deny",
            "rules": [
                {
                    "name": "insert-header",
                    "ruleType": "Fqdn",
                    "match": {"host": "httpbin.org"},
                    "action": {
                        "actionType": "Transform",
                        "headers": [
                            {
                                "name": "X-Custom-Tag",
                                "value": "my-value",
                                "operation": "Insert",
                            }
                        ],
                    },
                }
            ],
        },
        [("test headers to https://httpbin.org/headers", ("x-custom-tag",), ())],
        id="02-transform-insert",
    ),
    pytest.param(
        "e2e-dotnet-transform-set",
        {
            "mode": "Enforced",
            "defaultAction": "Deny",
            "rules": [
                {
                    "name": "set-user-agent",
                    "ruleType": "Fqdn",
                    "match": {"host": "httpbin.org"},
                    "action": {
                        "actionType": "Transform",
                        "headers": [
                            {
                                "name": "User-Agent",
                                "value": "policy-override-agent",
                                "operation": "Set",
                            }
                        ],
                    },
                }
            ],
        },
        [
            (
                "test headers to https://httpbin.org/headers",
                ("policy-override-agent",),
                (),
            )
        ],
        id="03-transform-set",
    ),
    pytest.param(
        "e2e-dotnet-transform-remove",
        {
            "mode": "Enforced",
            "defaultAction": "Deny",
            "rules": [
                {
                    "name": "remove-marker",
                    "ruleType": "Fqdn",
                    "match": {"host": "httpbin.org"},
                    "action": {
                        "actionType": "Transform",
                        "headers": [
                            {"name": "X-Test-Marker", "operation": "Remove"}
                        ],
                    },
                }
            ],
        },
        [
            (
                "test headers to https://httpbin.org/headers",
                (),
                ("x-test-marker",),
            )
        ],
        id="04-transform-remove",
    ),
    pytest.param(
        "e2e-dotnet-rewrite-host",
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
                        "rewrite": {
                            "scheme": "https",
                            "host": "www.bing.com",
                        },
                    },
                }
            ],
        },
        [("test egress to https://www.google.com", ("bing",), ())],
        id="05-rewrite-host",
    ),
    pytest.param(
        "e2e-dotnet-rewrite-path",
        {
            "mode": "Enforced",
            "defaultAction": "Deny",
            "rules": [
                {
                    "name": "rewrite-path",
                    "ruleType": "Fqdn",
                    "match": {"host": "httpbin.org", "path": "/get"},
                    "action": {
                        "actionType": "Rewrite",
                        "rewrite": {
                            "scheme": "https",
                            "host": "httpbin.org",
                            "path": "/ip",
                        },
                    },
                }
            ],
        },
        [
            (
                "test egress to https://httpbin.org/get",
                ("origin",),
                ('"url"', '"args"'),
            )
        ],
        id="06-rewrite-path",
    ),
    pytest.param(
        "e2e-dotnet-baseline",
        {
            "mode": "Enforced",
            "defaultAction": "Deny",
            "rules": [
                {
                    "name": "allow-all",
                    "ruleType": "Fqdn",
                    "match": {"host": "*"},
                    "action": {"actionType": "Allow"},
                }
            ],
        },
        [("test connectivity", ("status=200", "status=200"), ())],
        id="07-connectivity",
    ),
]


@pytest.mark.parametrize("name,policy,checks", CASES)
def test_basic_scenario(name, policy, checks):
    with deployed_policy(name, policy):
        for command, contains, excludes in checks:
            check_command(command, contains=contains, excludes=excludes)
