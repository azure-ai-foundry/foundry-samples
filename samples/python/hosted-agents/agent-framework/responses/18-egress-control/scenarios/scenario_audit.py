"""Audit mode E2E scenarios (Scenarios 13–15).

Audit mode semantics:
  - Deny rules are logged but NOT enforced — all traffic passes through.
  - Transform and Rewrite rules still apply, so audit mode gives observability
    without blocking traffic.

These scenarios validate that:
  - Allow rules work in audit mode (Test 14)
  - Deny rules pass through in audit mode (Test 13)
  - Audit vs Enforced produce different outcomes for the same deny rule (Test 15)

Run with:
    pytest scenarios/scenario_audit.py -v --tb=short

See conftest.py for required environment variables.
"""

import time

import pytest

from conftest import (
    create_egress_policy,
    delete_egress_policy,
    deploy_agent_version,
    invoke_agent,
)


# ── Test 13: Audit mode with deny rules (passthrough) ─────────────────────

class TestAuditDenyCurrentBehavior:
    """Test 13: Audit policy with Deny rules passes traffic through.

    Audit mode neutralizes deny rules — requests that would be blocked in
    Enforced mode succeed in Audit mode. This validates audit-mode passthrough.
    """

    POLICY_NAME = "e2e-test-audit-deny"

    @pytest.fixture(autouse=True, scope="class")
    def setup_policy(cls):
        create_egress_policy(cls.POLICY_NAME, {
            "mode": "Audit",
            "defaultAction": "Deny",
            "rules": [
                {"name": "allow-httpbin", "ruleType": "Fqdn",
                 "match": {"host": "httpbin.org"},
                 "action": {"actionType": "Allow"}},
                {"name": "deny-example", "ruleType": "Fqdn",
                 "match": {"host": "example.com"},
                 "action": {"actionType": "Deny"}},
                {"name": "deny-google", "ruleType": "Fqdn",
                 "match": {"host": "www.google.com"},
                 "action": {"actionType": "Deny"}},
            ],
        })
        deploy_agent_version(cls.POLICY_NAME)
        time.sleep(20)
        yield
        delete_egress_policy(cls.POLICY_NAME)

    def test_allowed_host_returns_200(self):
        """Allowed host should return 200 (same in both audit and enforced)."""
        result = invoke_agent("test egress to https://httpbin.org/get")
        # httpbin may return 503 transiently; retry once
        if "503" in result:
            time.sleep(15)
            result = invoke_agent("test egress to https://httpbin.org/get")
        assert "200" in result, f"Expected 200 for allowed host: {result[:200]}"

    def test_denied_host_passthrough_in_audit(self):
        """Denied host passes through in Audit mode (traffic allowed, logged).

        Audit mode neutralizes deny rules — requests succeed but are logged
        for observability. This validates audit-mode passthrough.
        """
        result = invoke_agent("test egress to https://example.com")
        assert "200" in result, \
            f"Expected 200 (audit passthrough): {result[:200]}"
        assert "403" not in result, \
            f"Deny should NOT be enforced in audit mode: {result[:200]}"

    def test_default_deny_passthrough_in_audit(self):
        """Host matching defaultAction=Deny also passes through in Audit mode."""
        result = invoke_agent("test egress to https://www.google.com")
        assert "200" in result or "301" in result or "302" in result, \
            f"Expected success (audit passthrough): {result[:200]}"
        assert "403" not in result, \
            f"Deny should NOT be enforced in audit mode: {result[:200]}"


# ── Test 14: Audit mode allow rules still work ────────────────────────────

class TestAuditAllowRules:
    """Test 14: Verify Allow rules work correctly under Audit mode.

    In audit mode, deny rules are logged but not enforced, so allow rules
    still function normally. This test uses an allow-all policy in audit mode.
    """

    POLICY_NAME = "e2e-test-audit-allow"

    @pytest.fixture(autouse=True, scope="class")
    def setup_policy(cls):
        create_egress_policy(cls.POLICY_NAME, {
            "mode": "Audit",
            "defaultAction": "Deny",
            "rules": [
                {"name": "allow-all", "ruleType": "Fqdn",
                 "match": {"host": "*"},
                 "action": {"actionType": "Allow"}},
            ],
        })
        deploy_agent_version(cls.POLICY_NAME)
        time.sleep(20)
        yield
        delete_egress_policy(cls.POLICY_NAME)

    def test_all_hosts_reachable_with_allow_all(self):
        """With allow-all rule, all hosts should be reachable in audit mode."""
        result = invoke_agent("test connectivity")
        count_200 = result.lower().count("200")
        assert count_200 >= 2, \
            f"Expected ≥2 hosts reachable with allow-all: {result[:300]}"


# ── Test 15: Audit vs Enforced produce same behavior (current) ────────────

class TestAuditVsEnforced:
    """Test 15: Verify Audit and Enforced modes differ for deny behavior.

    With audit passthrough, a deny rule is logged but not enforced in Audit
    mode, while the same rule blocks traffic in Enforced mode. This test
    asserts that difference: the denied host returns 200 under Audit and 403
    under Enforced.
    """

    AUDIT_POLICY = "e2e-test-audit-compare"
    ENFORCED_POLICY = "e2e-test-enforced-compare"

    @pytest.fixture(autouse=True, scope="class")
    def setup_policies(cls):
        rules = [
            {"name": "deny-example", "ruleType": "Fqdn",
             "match": {"host": "example.com"},
             "action": {"actionType": "Deny"}},
            {"name": "allow-all", "ruleType": "Fqdn",
             "match": {"host": "*"},
             "action": {"actionType": "Allow"}},
        ]
        create_egress_policy(cls.AUDIT_POLICY, {
            "mode": "Audit",
            "defaultAction": "Deny",
            "rules": rules,
        })
        create_egress_policy(cls.ENFORCED_POLICY, {
            "mode": "Enforced",
            "defaultAction": "Deny",
            "rules": rules,
        })
        yield
        delete_egress_policy(cls.AUDIT_POLICY)
        delete_egress_policy(cls.ENFORCED_POLICY)

    def test_audit_allows_enforced_denies(self):
        """Audit passes through, Enforced blocks — proves mode difference."""
        # Phase 1: Audit mode — should pass through
        deploy_agent_version(self.AUDIT_POLICY)
        time.sleep(20)

        audit_result = invoke_agent("test egress to https://example.com")

        # Phase 2: Enforced mode — should block
        deploy_agent_version(self.ENFORCED_POLICY)
        time.sleep(20)

        enforced_result = invoke_agent("test egress to https://example.com")

        # Audit: traffic allowed (passthrough)
        assert "200" in audit_result, \
            f"Expected 200 in audit (passthrough): {audit_result[:200]}"
        # Enforced: traffic blocked
        assert "403" in enforced_result, \
            f"Expected 403 in enforced: {enforced_result[:200]}"
