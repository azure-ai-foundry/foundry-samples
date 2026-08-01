"""Audit-mode egress scenarios 13–15 for the deployed .NET agent."""

import time

from conftest import (
    check_command,
    create_egress_policy,
    delete_egress_policy,
    deploy_agent_version,
    deployed_policy,
)


def test_13_audit_deny_passthrough():
    policy = {
        "mode": "Audit",
        "defaultAction": "Deny",
        "rules": [
            {
                "name": "allow-httpbin",
                "ruleType": "Fqdn",
                "match": {"host": "httpbin.org"},
                "action": {"actionType": "Allow"},
            },
            {
                "name": "deny-example",
                "ruleType": "Fqdn",
                "match": {"host": "example.com"},
                "action": {"actionType": "Deny"},
            },
        ],
    }
    with deployed_policy("e2e-dotnet-audit-deny", policy):
        check_command(
            "test egress to https://httpbin.org/get",
            contains=("200",),
        )
        check_command(
            "test egress to https://example.com",
            contains=("200",),
            excludes=("403",),
        )
        check_command(
            "test egress to https://www.google.com",
            contains_any=("200", "301", "302"),
            excludes=("403",),
        )


def test_14_audit_allow_all():
    policy = {
        "mode": "Audit",
        "defaultAction": "Deny",
        "rules": [
            {
                "name": "allow-all",
                "ruleType": "Fqdn",
                "match": {"host": "*"},
                "action": {"actionType": "Allow"},
            }
        ],
    }
    with deployed_policy("e2e-dotnet-audit-allow", policy):
        check_command(
            "test connectivity",
            contains=("status=200", "status=200"),
        )


def test_15_audit_vs_enforced():
    audit_name = "e2e-dotnet-audit-compare"
    enforced_name = "e2e-dotnet-enforced-compare"
    rules = [
        {
            "name": "deny-example",
            "ruleType": "Fqdn",
            "match": {"host": "example.com"},
            "action": {"actionType": "Deny"},
        },
        {
            "name": "allow-all",
            "ruleType": "Fqdn",
            "match": {"host": "*"},
            "action": {"actionType": "Allow"},
        },
    ]
    try:
        create_egress_policy(
            audit_name,
            {"mode": "Audit", "defaultAction": "Deny", "rules": rules},
        )
        create_egress_policy(
            enforced_name,
            {"mode": "Enforced", "defaultAction": "Deny", "rules": rules},
        )
        deploy_agent_version(audit_name)
        time.sleep(20)
        check_command(
            "test egress to https://example.com",
            contains=("200",),
            excludes=("403",),
        )

        deploy_agent_version(enforced_name)
        time.sleep(20)
        check_command(
            "test egress to https://example.com",
            contains=("403",),
        )
    finally:
        delete_egress_policy(audit_name)
        delete_egress_policy(enforced_name)
