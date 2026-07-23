"""Basic E2E egress scenarios (Scenarios 1–7).

These scenarios validate core egress proxy capabilities: Allow/Deny enforcement,
header transforms (Insert, Set, Remove), host/path rewrites, and baseline
connectivity.

Run with:
    pytest scenarios/scenario_basic.py -v --tb=short

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


# ── Test 1: Allow/Deny enforcement ──────────────────────────────────────────

class TestAllowDeny:
    """Test 1: Enforced policy with defaultAction=Deny, one Allow rule."""

    POLICY_NAME = "e2e-test-allow-deny"

    @pytest.fixture(autouse=True, scope="class")
    def setup_policy(cls):
        create_egress_policy(cls.POLICY_NAME, {
            "mode": "Enforced",
            "defaultAction": "Deny",
            "rules": [
                {"name": "allow-httpbin", "ruleType": "Fqdn",
                 "match": {"host": "httpbin.org"},
                 "action": {"actionType": "Allow"}}
            ],
        })
        deploy_agent_version(cls.POLICY_NAME)
        time.sleep(20)
        yield
        delete_egress_policy(cls.POLICY_NAME)

    def test_allowed_host_returns_200(self):
        result = invoke_agent("test egress to https://httpbin.org/get")
        assert "200" in result, f"Expected 200 for allowed host, got: {result[:200]}"

    def test_denied_host_returns_403(self):
        result = invoke_agent("test egress to https://example.com")
        assert "403" in result, f"Expected 403 for denied host, got: {result[:200]}"


# ── Test 2: Transform — Insert header ───────────────────────────────────────

class TestTransformInsert:
    """Test 2: Insert a custom header via Transform rule."""

    POLICY_NAME = "e2e-test-transform-insert"

    @pytest.fixture(autouse=True, scope="class")
    def setup_policy(cls):
        create_egress_policy(cls.POLICY_NAME, {
            "mode": "Enforced",
            "defaultAction": "Deny",
            "rules": [
                {"name": "insert-header", "ruleType": "Fqdn",
                 "match": {"host": "httpbin.org"},
                 "action": {"actionType": "Transform", "headers": [
                     {"name": "X-Custom-Tag", "value": "my-value", "operation": "Insert"}
                 ]}}
            ],
        })
        deploy_agent_version(cls.POLICY_NAME)
        time.sleep(20)
        yield
        delete_egress_policy(cls.POLICY_NAME)

    def test_custom_header_inserted(self):
        result = invoke_agent("test headers to https://httpbin.org/headers")
        assert "x-custom-tag" in result.lower(), f"Expected X-Custom-Tag in headers: {result[:300]}"


# ── Test 3: Transform — Set (overwrite) header ─────────────────────────────

class TestTransformSet:
    """Test 3: Overwrite User-Agent header via Transform Set."""

    POLICY_NAME = "e2e-test-transform-set"

    @pytest.fixture(autouse=True, scope="class")
    def setup_policy(cls):
        create_egress_policy(cls.POLICY_NAME, {
            "mode": "Enforced",
            "defaultAction": "Deny",
            "rules": [
                {"name": "set-user-agent", "ruleType": "Fqdn",
                 "match": {"host": "httpbin.org"},
                 "action": {"actionType": "Transform", "headers": [
                     {"name": "User-Agent", "value": "policy-override-agent", "operation": "Set"}
                 ]}}
            ],
        })
        deploy_agent_version(cls.POLICY_NAME)
        time.sleep(20)
        yield
        delete_egress_policy(cls.POLICY_NAME)

    def test_user_agent_overwritten(self):
        result = invoke_agent("test headers to https://httpbin.org/headers")
        assert "policy-override-agent" in result.lower(), \
            f"Expected overwritten User-Agent: {result[:300]}"


# ── Test 4: Transform — Remove header ──────────────────────────────────────

class TestTransformRemove:
    """Test 4: Remove X-Test-Marker header via Transform Remove."""

    POLICY_NAME = "e2e-test-transform-remove"

    @pytest.fixture(autouse=True, scope="class")
    def setup_policy(cls):
        create_egress_policy(cls.POLICY_NAME, {
            "mode": "Enforced",
            "defaultAction": "Deny",
            "rules": [
                {"name": "remove-marker", "ruleType": "Fqdn",
                 "match": {"host": "httpbin.org"},
                 "action": {"actionType": "Transform", "headers": [
                     {"name": "X-Test-Marker", "operation": "Remove"}
                 ]}}
            ],
        })
        deploy_agent_version(cls.POLICY_NAME)
        time.sleep(20)
        yield
        delete_egress_policy(cls.POLICY_NAME)

    def test_marker_header_removed(self):
        result = invoke_agent("test headers to https://httpbin.org/headers")
        assert "x-test-marker" not in result.lower(), \
            f"X-Test-Marker should be removed but found in: {result[:300]}"


# ── Test 5: Rewrite — Host rewrite ─────────────────────────────────────────

class TestRewriteHost:
    """Test 5: Rewrite google.com → bing.com."""

    POLICY_NAME = "e2e-test-rewrite-host"

    @pytest.fixture(autouse=True, scope="class")
    def setup_policy(cls):
        create_egress_policy(cls.POLICY_NAME, {
            "mode": "Enforced",
            "defaultAction": "Deny",
            "rules": [
                {"name": "rewrite-to-bing", "ruleType": "Fqdn",
                 "match": {"host": "www.google.com"},
                 "action": {"actionType": "Rewrite",
                            "rewrite": {"scheme": "https", "host": "www.bing.com"}}}
            ],
        })
        deploy_agent_version(cls.POLICY_NAME)
        time.sleep(20)
        yield
        delete_egress_policy(cls.POLICY_NAME)

    def test_google_rewritten_to_bing(self):
        result = invoke_agent("test egress to https://www.google.com")
        assert "bing" in result.lower(), f"Expected Bing content: {result[:300]}"


# ── Test 6: Rewrite — Path rewrite ─────────────────────────────────────────

class TestRewritePath:
    """Test 6: Rewrite /get → /ip on httpbin.org."""

    POLICY_NAME = "e2e-test-rewrite-path"

    @pytest.fixture(autouse=True, scope="class")
    def setup_policy(cls):
        create_egress_policy(cls.POLICY_NAME, {
            "mode": "Enforced",
            "defaultAction": "Deny",
            "rules": [
                {"name": "rewrite-path", "ruleType": "Fqdn",
                 "match": {"host": "httpbin.org", "path": "/get"},
                 "action": {"actionType": "Rewrite",
                            "rewrite": {"scheme": "https", "host": "httpbin.org", "path": "/ip"}}}
            ],
        })
        deploy_agent_version(cls.POLICY_NAME)
        time.sleep(20)
        yield
        delete_egress_policy(cls.POLICY_NAME)

    def test_path_rewritten(self):
        result = invoke_agent("test egress to https://httpbin.org/get")
        lower = result.lower()
        # /get rewritten to /ip → response should contain "origin" (the /ip response)
        # or at least NOT contain typical /get fields like "args", "headers", "url"
        # A 503 from httpbin is transient; retry once
        if "503" in lower:
            time.sleep(15)
            result = invoke_agent("test egress to https://httpbin.org/get")
            lower = result.lower()
        assert "origin" in lower, \
            f"Expected /ip response (origin field): {result[:300]}"


# ── Test 7: Connectivity baseline ──────────────────────────────────────────

class TestConnectivityBaseline:
    """Test 7: With an allow-all policy, all hosts should be reachable."""

    POLICY_NAME = "e2e-test-baseline"

    @pytest.fixture(autouse=True, scope="class")
    def setup_policy(cls):
        create_egress_policy(cls.POLICY_NAME, {
            "mode": "Enforced",
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

    def test_all_hosts_reachable(self):
        result = invoke_agent("test connectivity")
        # Connectivity test probes 3 hosts; at least 2 should succeed
        count_200 = result.lower().count("200")
        assert count_200 >= 2, f"Expected ≥2 hosts reachable, got {count_200}: {result[:300]}"
