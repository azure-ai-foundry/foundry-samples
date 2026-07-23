"""Advanced E2E egress scenarios (Scenarios 8–12).

These scenarios validate edge cases and rule interactions: first-match semantics,
multiple transforms, combined rewrite+transform, deny-all, and wildcard matching.

Run with:
    pytest scenarios/scenario_advanced.py -v --tb=short

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


# ── Test 8: First-match rule ordering ───────────────────────────────────────

class TestFirstMatchOrdering:
    """Test 8: the egress proxy uses first-match semantics — earlier rules take priority."""

    POLICY_NAME = "e2e-test-first-match"

    @pytest.fixture(autouse=True, scope="class")
    def setup_policy(cls):
        create_egress_policy(cls.POLICY_NAME, {
            "mode": "Enforced",
            "defaultAction": "Deny",
            "rules": [
                {"name": "deny-httpbin-ip", "ruleType": "Fqdn",
                 "match": {"host": "httpbin.org", "path": "/ip"},
                 "action": {"actionType": "Deny"}},
                {"name": "allow-httpbin-all", "ruleType": "Fqdn",
                 "match": {"host": "httpbin.org"},
                 "action": {"actionType": "Allow"}},
            ],
        })
        deploy_agent_version(cls.POLICY_NAME)
        time.sleep(20)
        yield
        delete_egress_policy(cls.POLICY_NAME)

    def test_general_path_allowed_by_second_rule(self):
        """GET /get matches 'allow-httpbin-all' (2nd rule) → 200."""
        result = invoke_agent("test egress to https://httpbin.org/get")
        assert "200" in result, f"Expected 200 for /get: {result[:200]}"

    def test_specific_path_denied_by_first_rule(self):
        """GET /ip matches 'deny-httpbin-ip' (1st rule) → 403."""
        result = invoke_agent("test egress to https://httpbin.org/ip")
        assert "403" in result, f"Expected 403 for /ip: {result[:200]}"


# ── Test 9: Multiple transforms in one rule ─────────────────────────────────

class TestMultiTransform:
    """Test 9: Insert, Set, and Remove in a single Transform rule."""

    POLICY_NAME = "e2e-test-multi-transform"

    @pytest.fixture(autouse=True, scope="class")
    def setup_policy(cls):
        create_egress_policy(cls.POLICY_NAME, {
            "mode": "Enforced",
            "defaultAction": "Deny",
            "rules": [
                {"name": "multi-transform", "ruleType": "Fqdn",
                 "match": {"host": "httpbin.org"},
                 "action": {"actionType": "Transform", "headers": [
                     {"name": "X-Custom-Inserted", "value": "hello", "operation": "Insert"},
                     {"name": "User-Agent", "value": "policy-agent/1.0", "operation": "Set"},
                     {"name": "X-Test-Marker", "operation": "Remove"},
                 ]}},
            ],
        })
        deploy_agent_version(cls.POLICY_NAME)
        time.sleep(20)
        yield
        delete_egress_policy(cls.POLICY_NAME)

    def test_custom_header_inserted(self):
        result = invoke_agent("test headers to https://httpbin.org/headers")
        assert "x-custom-inserted" in result.lower(), \
            f"Expected X-Custom-Inserted: {result[:300]}"

    def test_user_agent_overwritten(self):
        result = invoke_agent("test headers to https://httpbin.org/headers")
        assert "policy-agent" in result.lower(), \
            f"Expected policy-agent UA: {result[:300]}"

    def test_marker_removed(self):
        result = invoke_agent("test headers to https://httpbin.org/headers")
        assert "x-test-marker" not in result.lower(), \
            f"X-Test-Marker should be removed: {result[:300]}"


# ── Test 10: Combined Rewrite + Transform (separate rules) ─────────────────

class TestRewritePlusTransform:
    """Test 10: Rewrite and Transform as separate rules in one policy."""

    POLICY_NAME = "e2e-test-rewrite-transform"

    @pytest.fixture(autouse=True, scope="class")
    def setup_policy(cls):
        create_egress_policy(cls.POLICY_NAME, {
            "mode": "Enforced",
            "defaultAction": "Deny",
            "rules": [
                {"name": "rewrite-to-bing", "ruleType": "Fqdn",
                 "match": {"host": "www.google.com"},
                 "action": {"actionType": "Rewrite",
                            "rewrite": {"scheme": "https", "host": "www.bing.com"}}},
                {"name": "transform-httpbin", "ruleType": "Fqdn",
                 "match": {"host": "httpbin.org"},
                 "action": {"actionType": "Transform", "headers": [
                     {"name": "X-Policy-Tag", "value": "tagged", "operation": "Insert"}
                 ]}},
            ],
        })
        deploy_agent_version(cls.POLICY_NAME)
        time.sleep(20)
        yield
        delete_egress_policy(cls.POLICY_NAME)

    def test_google_rewritten_to_bing(self):
        result = invoke_agent("test egress to https://www.google.com")
        if "429" in result or "rate_limit" in result.lower():
            time.sleep(30)
            result = invoke_agent("test egress to https://www.google.com")
        assert "bing" in result.lower(), f"Expected Bing content: {result[:300]}"

    def test_httpbin_header_tagged(self):
        result = invoke_agent("test headers to https://httpbin.org/headers")
        assert "x-policy-tag" in result.lower(), \
            f"Expected X-Policy-Tag: {result[:300]}"

    def test_unmatched_host_denied(self):
        result = invoke_agent("test egress to https://example.com")
        assert "403" in result, f"Expected 403 for example.com: {result[:200]}"


# ── Test 11: Deny-all (no rules) ───────────────────────────────────────────

class TestDenyAll:
    """Test 11: Enforced + Deny + no rules = block everything.

    Note: This requires full traffic inspection, which the Foundry
    platform sets this correctly when mode=Enforced.
    """

    POLICY_NAME = "e2e-test-deny-all"

    @pytest.fixture(autouse=True, scope="class")
    def setup_policy(cls):
        create_egress_policy(cls.POLICY_NAME, {
            "mode": "Enforced",
            "defaultAction": "Deny",
            "rules": [],
        })
        deploy_agent_version(cls.POLICY_NAME)
        time.sleep(20)
        yield
        delete_egress_policy(cls.POLICY_NAME)

    def test_all_traffic_blocked(self):
        result = invoke_agent("test connectivity")
        lower = result.lower()
        # Distinguish an egress denial from an unrelated failure. invoke_agent()
        # returns an "ERROR: ..." string for model, auth, or rate-limit failures;
        # those do NOT prove the egress policy blocked traffic, so fail loudly
        # rather than treating them as a pass.
        if result.startswith("ERROR:"):
            pytest.fail(
                f"Agent call failed before egress could be evaluated "
                f"(model/auth/rate-limit, not an egress denial): {result[:300]}"
            )
        # Under deny-all the outbound probes must be blocked: no target may
        # report a 2xx success, and httpbin.org (reachable under an Allow
        # policy) must now show a 403/Forbidden egress denial.
        assert "✅" not in result, \
            f"Expected all egress blocked, but a target succeeded: {result[:400]}"
        assert "403" in lower or "forbidden" in lower, \
            f"Expected egress denial (403/Forbidden) for blocked traffic: {result[:400]}"


# ── Test 12: Wildcard host matching ────────────────────────────────────────

class TestWildcardHost:
    """Test 12: *.org should match httpbin.org but not example.com."""

    POLICY_NAME = "e2e-test-wildcard"

    @pytest.fixture(autouse=True, scope="class")
    def setup_policy(cls):
        create_egress_policy(cls.POLICY_NAME, {
            "mode": "Enforced",
            "defaultAction": "Deny",
            "rules": [
                {"name": "allow-dot-org", "ruleType": "Fqdn",
                 "match": {"host": "*.org"},
                 "action": {"actionType": "Allow"}},
            ],
        })
        deploy_agent_version(cls.POLICY_NAME)
        time.sleep(20)
        yield
        delete_egress_policy(cls.POLICY_NAME)

    def test_org_domain_allowed(self):
        result = invoke_agent("test egress to https://httpbin.org/get")
        assert "200" in result, f"Expected 200 for *.org match: {result[:200]}"

    def test_com_domain_denied(self):
        result = invoke_agent("test egress to https://example.com")
        assert "403" in result, f"Expected 403 for .com: {result[:200]}"
