"""ManagedIdentityRef E2E scenarios (Scenarios 16–17).

These scenarios validate that the egress proxy can inject Azure AD tokens from the
sandbox's managed identity into outbound requests via Transform rules with
managedIdentityRef.  The proxy acquires a token for the specified resource
audience and injects it as a header — agent code never handles credentials.

Prerequisites:
    1. A storage account accessible from the sandbox network
    2. The deployed agent's instance identity must have "Storage Blob Data
       Contributor" (or Reader) on the storage account
    3. A blob container must exist (default: "egress-test")
    4. Set AGENT_STORAGE_ACCOUNT env var to the storage account name

Setup:
    # Deploy the agent first, then find its instance identity principal ID
    azd ai agent show --output json \\
      | jq -r ".instance_identity.principal_id"

    # Create storage account + container
    az storage account create -n <name> -g <rg> --sku Standard_LRS
    az storage container create -n egress-test --account-name <name>

    # Assign RBAC
    az role assignment create \\
      --assignee-object-id <principalId> \\
      --assignee-principal-type ServicePrincipal \\
      --role "Storage Blob Data Contributor" \\
      --scope "/subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.Storage/storageAccounts/<name>"

Run with:
    pytest scenarios/scenario_managed_identity.py -v --tb=short

See conftest.py for required environment variables.
"""

import time

import pytest

from conftest import (
    AGENT_STORAGE_ACCOUNT,
    AGENT_STORAGE_CONTAINER,
    create_egress_policy,
    delete_egress_policy,
    deploy_agent_version,
    invoke_agent,
)

def _skip_if_no_storage():
    if not AGENT_STORAGE_ACCOUNT:
        pytest.skip("AGENT_STORAGE_ACCOUNT not set — skipping MI tests")


# ── Test 16: MI token injection to Azure Blob Storage ──────────────────────

class TestManagedIdentityStorageAccess:
    """Test 16: Transform rule injects MI token → agent accesses storage.

    Creates a policy with a Transform rule that uses managedIdentityRef to
    inject an Authorization header with a token scoped to
    https://storage.azure.com/.  The agent makes a request to the
    storage account's blob REST API and should succeed (200/OK) because the
    proxy acquired and injected the MI token.
    """

    POLICY_NAME = "e2e-test-mi-storage"

    @pytest.fixture(autouse=True, scope="class")
    def setup_policy(cls):
        _skip_if_no_storage()
        storage_host = f"{AGENT_STORAGE_ACCOUNT}.blob.core.windows.net"
        create_egress_policy(cls.POLICY_NAME, {
            "mode": "Enforced",
            "defaultAction": "Deny",
            "rules": [
                # MI token injection for storage
                {"name": "mi-storage-token", "ruleType": "Fqdn",
                 "match": {"host": storage_host},
                 "action": {"actionType": "Transform", "headers": [
                     {"name": "Authorization", "operation": "Set",
                      "valueRef": {
                          "managedIdentityRef": {
                              "resource": "https://storage.azure.com/",
                              "format": "Bearer {value}",
                          }
                      }},
                     # Storage REST API requires x-ms-version
                     {"name": "x-ms-version", "value": "2023-11-03",
                      "operation": "Set"},
                 ]}},
                # Allow httpbin for verification (no MI)
                {"name": "allow-httpbin", "ruleType": "Fqdn",
                 "match": {"host": "httpbin.org"},
                 "action": {"actionType": "Allow"}},
            ],
        })
        deploy_agent_version(cls.POLICY_NAME)
        time.sleep(20)
        yield
        delete_egress_policy(cls.POLICY_NAME)

    def test_storage_list_blobs_succeeds(self):
        """Agent requests blob list → proxy injects MI token → 200 OK."""
        storage_url = (
            f"https://{AGENT_STORAGE_ACCOUNT}.blob.core.windows.net"
            f"/{AGENT_STORAGE_CONTAINER}?restype=container&comp=list"
        )
        result = invoke_agent(f"test egress to {storage_url}")
        # Successful storage response returns 200 with XML body containing
        # <EnumerationResults> or empty <Blobs/> list
        lower = result.lower()
        assert "200" in lower or "enumerationresults" in lower or "blobs" in lower, \
            f"Expected successful storage list, got: {result[:400]}"
        # Should NOT get 403 (AuthorizationFailure) or 401 (no token)
        assert "authorizationfailure" not in lower and "401" not in result, \
            f"Storage auth failed — check MI RBAC: {result[:400]}"

    def test_storage_get_container_props(self):
        """Agent requests container properties → proxy injects MI token."""
        storage_url = (
            f"https://{AGENT_STORAGE_ACCOUNT}.blob.core.windows.net"
            f"/{AGENT_STORAGE_CONTAINER}?restype=container"
        )
        result = invoke_agent(f"test egress to {storage_url}")
        lower = result.lower()
        # Container properties returns 200 with headers like x-ms-lease-state
        assert "200" in lower or "lease-state" in lower, \
            f"Expected container properties, got: {result[:400]}"


# ── Test 17: MI token NOT sent to non-matching hosts ───────────────────────

class TestManagedIdentityHostScoping:
    """Test 17: MI token is only injected for matching host.

    The same policy from Test 16 allows httpbin.org but WITHOUT MI injection.
    Requests to httpbin should NOT contain an Authorization header with a
    Bearer token — proving the MI Transform is host-scoped.
    """

    POLICY_NAME = "e2e-test-mi-scoping"

    @pytest.fixture(autouse=True, scope="class")
    def setup_policy(cls):
        _skip_if_no_storage()
        storage_host = f"{AGENT_STORAGE_ACCOUNT}.blob.core.windows.net"
        create_egress_policy(cls.POLICY_NAME, {
            "mode": "Enforced",
            "defaultAction": "Deny",
            "rules": [
                # MI token for storage only
                {"name": "mi-storage-token", "ruleType": "Fqdn",
                 "match": {"host": storage_host},
                 "action": {"actionType": "Transform", "headers": [
                     {"name": "Authorization", "operation": "Set",
                      "valueRef": {
                          "managedIdentityRef": {
                              "resource": "https://storage.azure.com/",
                              "format": "Bearer {value}",
                          }
                      }},
                 ]}},
                # Allow httpbin WITHOUT MI (plain Allow)
                {"name": "allow-httpbin", "ruleType": "Fqdn",
                 "match": {"host": "httpbin.org"},
                 "action": {"actionType": "Allow"}},
            ],
        })
        deploy_agent_version(cls.POLICY_NAME)
        time.sleep(20)
        yield
        delete_egress_policy(cls.POLICY_NAME)

    def test_httpbin_no_bearer_token(self):
        """httpbin headers echo should NOT contain a Bearer token."""
        result = invoke_agent("test headers to https://httpbin.org/headers")
        lower = result.lower()
        # httpbin echoes all request headers — Authorization: Bearer should
        # NOT appear because the MI Transform only matches the storage host
        assert "bearer" not in lower or "authorization" not in lower, \
            f"MI token should NOT be injected for httpbin: {result[:400]}"

    def test_httpbin_still_accessible(self):
        """httpbin should still be accessible (Allow rule works)."""
        result = invoke_agent("test egress to https://httpbin.org/get")
        assert "200" in result, f"Expected 200 for httpbin: {result[:200]}"
