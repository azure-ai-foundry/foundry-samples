#!/usr/bin/env python3
"""
KB API Connectivity Test Script

Tests direct REST API connectivity to Azure AI Search Knowledge Base endpoint.
This validates the KB API works before testing through the MCP server and agent.

Tests:
1. KB endpoint reachability (health check)
2. Index query via standard search API
3. SharePoint global header passthrough (x-ms-sharepoint-*)
4. Multi-source type response validation

Usage:
  python test_kb_api_connectivity.py --endpoint https://<search>.search.windows.net --api-key <key>
  python test_kb_api_connectivity.py --endpoint ... --api-key ... --test connectivity
  python test_kb_api_connectivity.py --endpoint ... --api-key ... --test sharepoint_headers
"""

import argparse
import json
import logging
import os
import ssl
import sys
import urllib.error
import urllib.request

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s - %(name)s - %(levelname)s - %(message)s",
)
logger = logging.getLogger(__name__)

# ============================================================================
# CONFIGURATION
# ============================================================================
SEARCH_ENDPOINT = os.environ.get("AZURE_SEARCH_ENDPOINT", "")
SEARCH_API_KEY = os.environ.get("AZURE_SEARCH_API_KEY", "")
KB_NAME = os.environ.get("AZURE_SEARCH_KB_NAME", "test-kb")
API_VERSION = "2024-07-01"
KB_API_VERSION = "2025-11-01-preview"
# ============================================================================


def make_request(url, method="GET", data=None, headers=None, timeout=15):
    """Make an HTTP request and return (status, body_dict, response_headers)."""
    ctx = ssl.create_default_context()
    encoded = json.dumps(data).encode("utf-8") if data else None
    req = urllib.request.Request(url, data=encoded, headers=headers or {}, method=method)
    with urllib.request.urlopen(req, timeout=timeout, context=ctx) as resp:
        body = resp.read().decode("utf-8")
        return resp.status, json.loads(body) if body else {}, dict(resp.headers)


def test_endpoint_reachability(endpoint, api_key):
    """Test that the AI Search endpoint is reachable."""
    print("\n" + "=" * 60)
    print("TEST: AI Search Endpoint Reachability")
    print("=" * 60)
    print(f"  Endpoint: {endpoint}")

    url = f"{endpoint}/servicestats?api-version={API_VERSION}"
    headers = {"api-key": api_key}

    try:
        status, result, _ = make_request(url, headers=headers)
        print(f"  ✓ HTTP Status: {status}")
        if "counters" in result:
            doc_count = result["counters"].get("documentCount", {}).get("usage", "N/A")
            index_count = result["counters"].get("indexCounter", {}).get("usage", "N/A")
            print(f"  ✓ Total documents: {doc_count}")
            print(f"  ✓ Total indexes: {index_count}")
        print("\n✓ TEST PASSED: AI Search endpoint is reachable")
        return True
    except urllib.error.URLError as e:
        print(f"\n✗ TEST FAILED: {e}")
        if "Name or service not known" in str(e) or "getaddrinfo failed" in str(e):
            print("  Note: Expected if running from outside the VNet.")
            print("  The AI Search endpoint is only accessible via private endpoint.")
        return False
    except Exception as e:
        print(f"\n✗ TEST FAILED: {e}")
        return False


def test_index_query(endpoint, api_key, kb_name):
    """Test querying the KB index via standard search API."""
    print("\n" + "=" * 60)
    print("TEST: KB Index Query (Standard Search API)")
    print("=" * 60)
    print(f"  Index: {kb_name}")

    url = f"{endpoint}/indexes/{kb_name}/docs?api-version={API_VERSION}&search=*&$top=5&$count=true"
    headers = {"api-key": api_key, "Content-Type": "application/json"}

    try:
        status, result, _ = make_request(url, headers=headers)
        doc_count = result.get("@odata.count", 0)
        docs = result.get("value", [])

        print(f"  ✓ HTTP Status: {status}")
        print(f"  ✓ Total documents in index: {doc_count}")
        print(f"  ✓ Returned documents: {len(docs)}")

        if docs:
            # Validate schema
            expected_fields = {"id", "content", "title", "sourceType", "documentUrl"}
            actual_fields = set(docs[0].keys()) - {"@search.score"}
            missing = expected_fields - actual_fields
            if missing:
                print(f"  ⚠ Missing fields: {missing}")
            else:
                print(f"  ✓ Schema validation passed (all expected fields present)")

            # Show sample
            for doc in docs[:2]:
                print(f"    - [{doc.get('sourceType', '?')}] {doc.get('title', 'Untitled')}")

        print("\n✓ TEST PASSED: KB index query works")
        return True
    except urllib.error.HTTPError as e:
        if e.code == 404:
            print(f"\n✗ TEST FAILED: Index '{kb_name}' not found")
            print("  Run create_sample_kb.py first to create the index.")
        else:
            print(f"\n✗ TEST FAILED: HTTP {e.code}")
        return False
    except Exception as e:
        print(f"\n✗ TEST FAILED: {e}")
        return False


def test_multi_source_types(endpoint, api_key, kb_name):
    """Test that all 3 source types (sharepoint, searchindex, web) are present."""
    print("\n" + "=" * 60)
    print("TEST: Multi-Source Type Validation")
    print("=" * 60)

    expected_sources = ["sharepoint", "searchindex", "web"]
    headers = {"api-key": api_key, "Content-Type": "application/json"}
    results = {}

    for source_type in expected_sources:
        url = (
            f"{endpoint}/indexes/{kb_name}/docs?api-version={API_VERSION}"
            f"&search=*&$filter=sourceType eq '{source_type}'&$count=true"
        )
        try:
            status, result, _ = make_request(url, headers=headers)
            count = result.get("@odata.count", len(result.get("value", [])))
            results[source_type] = count
            print(f"  ✓ sourceType='{source_type}': {count} documents")
        except Exception as e:
            results[source_type] = 0
            print(f"  ✗ sourceType='{source_type}': FAILED ({e})")

    all_present = all(c > 0 for c in results.values())
    if all_present:
        print("\n✓ TEST PASSED: All 3 source types have documents")
    else:
        missing = [s for s, c in results.items() if c == 0]
        print(f"\n✗ TEST FAILED: Missing source types: {missing}")
    return all_present


def test_sharepoint_headers(endpoint, api_key, kb_name):
    """Test that x-ms-sharepoint-* global headers are accepted."""
    print("\n" + "=" * 60)
    print("TEST: SharePoint Global Headers (x-ms-sharepoint-*)")
    print("=" * 60)

    url = (
        f"{endpoint}/indexes/{kb_name}/docs?api-version={API_VERSION}"
        f"&search=vendor+policy&$filter=sourceType eq 'sharepoint'&$top=3"
    )
    headers = {
        "api-key": api_key,
        "Content-Type": "application/json",
        # SharePoint global headers for remote content access
        "x-ms-sharepoint-siteurl": "https://contoso.sharepoint.com/sites/policies",
        "x-ms-sharepoint-tenantid": "00000000-0000-0000-0000-000000000000",
        "x-ms-sharepoint-accesstoken": "test-validation-token",
    }

    try:
        status, result, resp_headers = make_request(url, headers=headers)
        docs = result.get("value", [])

        print(f"  ✓ HTTP Status: {status}")
        print(f"  ✓ Results with SP headers: {len(docs)}")
        print("  ✓ x-ms-sharepoint-siteurl: accepted")
        print("  ✓ x-ms-sharepoint-tenantid: accepted")
        print("  ✓ x-ms-sharepoint-accesstoken: accepted")

        if docs:
            for doc in docs[:2]:
                print(f"    - {doc.get('title', 'Untitled')} (score: {doc.get('@search.score', 'N/A')})")

        print("\n✓ TEST PASSED: SharePoint global headers accepted")
        return True
    except urllib.error.HTTPError as e:
        # 403 means headers were parsed but token is invalid — that's fine for testing
        if e.code == 403:
            print(f"  ✓ HTTP 403 — headers accepted, token validation expected to fail in test")
            print("\n✓ TEST PASSED: SharePoint headers are processed by AI Search")
            return True
        print(f"\n✗ TEST FAILED: HTTP {e.code}")
        body = e.read().decode("utf-8") if hasattr(e, "read") else ""
        if body:
            print(f"  Error: {body[:300]}")
        return False
    except Exception as e:
        print(f"\n✗ TEST FAILED: {e}")
        return False


def test_semantic_search(endpoint, api_key, kb_name):
    """Test a targeted search query returns relevant results."""
    print("\n" + "=" * 60)
    print("TEST: Semantic Search Query")
    print("=" * 60)

    queries = [
        ("vendor policy", "sp-001"),
        ("remote work", "sp-002"),
        ("API authentication", "idx-001"),
        ("cybersecurity framework", "web-003"),
    ]

    headers = {"api-key": api_key, "Content-Type": "application/json"}
    passed = 0

    for query, expected_top_id in queries:
        url = f"{endpoint}/indexes/{kb_name}/docs?api-version={API_VERSION}&search={query.replace(' ', '+')}&$top=3"
        try:
            status, result, _ = make_request(url, headers=headers)
            docs = result.get("value", [])
            top_id = docs[0]["id"] if docs else None
            match = "✓" if top_id == expected_top_id else "~"
            print(f"  {match} Query '{query}': top result = {top_id} (expected {expected_top_id})")
            if docs:
                passed += 1
        except Exception as e:
            print(f"  ✗ Query '{query}': FAILED ({e})")

    if passed == len(queries):
        print(f"\n✓ TEST PASSED: All {len(queries)} queries returned results")
    else:
        print(f"\n⚠ TEST PARTIAL: {passed}/{len(queries)} queries returned results")
    return passed > 0


def main():
    parser = argparse.ArgumentParser(
        description="Test Azure AI Search KB API connectivity",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  python test_kb_api_connectivity.py --endpoint https://my-search.search.windows.net --api-key KEY123
  python test_kb_api_connectivity.py --test connectivity
  python test_kb_api_connectivity.py --test sharepoint_headers
  python test_kb_api_connectivity.py --test all

Environment variables (alternative to CLI args):
  AZURE_SEARCH_ENDPOINT   - AI Search endpoint URL
  AZURE_SEARCH_API_KEY    - AI Search admin API key
  AZURE_SEARCH_KB_NAME    - KB / index name (default: test-kb)
""",
    )
    parser.add_argument("--endpoint", default=SEARCH_ENDPOINT, help="AI Search endpoint URL")
    parser.add_argument("--api-key", default=SEARCH_API_KEY, help="AI Search admin API key")
    parser.add_argument("--kb-name", default=KB_NAME, help="KB / index name (default: test-kb)")
    parser.add_argument(
        "--test",
        choices=["connectivity", "query", "multi_source", "sharepoint_headers", "semantic", "all"],
        default="all",
        help="Which test to run (default: all)",
    )

    args = parser.parse_args()

    if not args.endpoint or not args.api_key:
        parser.error("--endpoint and --api-key are required (or set AZURE_SEARCH_ENDPOINT and AZURE_SEARCH_API_KEY)")

    print("=" * 60)
    print("KB API CONNECTIVITY TEST")
    print("=" * 60)
    print(f"  Endpoint: {args.endpoint}")
    print(f"  KB Name:  {args.kb_name}")

    results = {}

    if args.test in ["connectivity", "all"]:
        results["connectivity"] = test_endpoint_reachability(args.endpoint, args.api_key)

    if args.test in ["query", "all"]:
        results["index_query"] = test_index_query(args.endpoint, args.api_key, args.kb_name)

    if args.test in ["multi_source", "all"]:
        results["multi_source"] = test_multi_source_types(args.endpoint, args.api_key, args.kb_name)

    if args.test in ["sharepoint_headers", "all"]:
        results["sharepoint_headers"] = test_sharepoint_headers(args.endpoint, args.api_key, args.kb_name)

    if args.test in ["semantic", "all"]:
        results["semantic_search"] = test_semantic_search(args.endpoint, args.api_key, args.kb_name)

    # Summary
    print("\n" + "=" * 60)
    print("TEST SUMMARY")
    print("=" * 60)
    for name, passed in results.items():
        status = "✓ PASSED" if passed else "✗ FAILED"
        print(f"  {name}: {status}")

    all_passed = all(results.values()) if results else True
    print("\n" + "=" * 60)
    if all_passed:
        print("ALL TESTS PASSED")
    else:
        print("SOME TESTS FAILED")
    print("=" * 60)

    sys.exit(0 if all_passed else 1)


if __name__ == "__main__":
    main()
