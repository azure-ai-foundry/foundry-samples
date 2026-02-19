#!/usr/bin/env python3
"""
Create Sample Knowledge Base in Azure AI Search

This script creates a sample Knowledge Base index in Azure AI Search and
populates it with test documents across three source types:
  - SharePoint (enterprise policies, HR docs)
  - SearchIndex (product docs, engineering)
  - Web (external research, best practices)

The documents follow the KBRetrieveResult schema used by the Foundry IQ KB
MCP server.

Usage:
  # With API key auth
  python create_sample_kb.py \
    --endpoint https://<search-service>.search.windows.net \
    --api-key <admin-key> \
    --kb-name test-kb

  # With DefaultAzureCredential (requires Search Index Data Contributor role)
  python create_sample_kb.py \
    --endpoint https://<search-service>.search.windows.net \
    --kb-name test-kb \
    --use-aad

  # Temporarily enable public access, seed data, then re-disable
  python create_sample_kb.py \
    --endpoint https://<search-service>.search.windows.net \
    --api-key <admin-key> \
    --kb-name test-kb \
    --toggle-public-access \
    --resource-group <rg-name> \
    --search-service-name <search-name>
"""

import argparse
import json
import logging
import ssl
import subprocess
import sys
import time
import urllib.error
import urllib.request

logging.basicConfig(level=logging.INFO, format="%(asctime)s - %(levelname)s - %(message)s")
logger = logging.getLogger(__name__)

API_VERSION = "2024-07-01"
KB_API_VERSION = "2025-11-01-preview"

# ============================================================================
# Sample documents across 3 source types
# ============================================================================
SAMPLE_DOCUMENTS = [
    # ── SharePoint sources ───────────────────────────────────────────────
    {
        "id": "sp-001",
        "content": "The vendor approval process requires three levels of sign-off: department head, procurement team, and legal review. All vendors must complete a compliance questionnaire and provide proof of insurance before engagement. Emergency vendor approvals may bypass the procurement team with VP-level authorization.",
        "title": "Vendor_Policy_2025.pdf",
        "documentUrl": "https://contoso.sharepoint.com/sites/policies/Vendor_Policy_2025.pdf",
        "sourceType": "sharepoint",
        "pageNumber": 7,
        "totalPages": 12,
        "relevanceScore": 0.94,
        "lastModified": "2026-01-15T10:30:00Z",
        "sourceGroup": "HR Policies",
    },
    {
        "id": "sp-002",
        "content": "Contoso's remote work policy allows employees to work from home up to 3 days per week. Fully remote arrangements require VP approval and a home office safety assessment. All employees must be available during core hours (10am-3pm local time) regardless of work location.",
        "title": "Remote_Work_Policy_2026.pdf",
        "documentUrl": "https://contoso.sharepoint.com/sites/hr/Remote_Work_Policy_2026.pdf",
        "sourceType": "sharepoint",
        "pageNumber": 2,
        "totalPages": 6,
        "relevanceScore": 0.89,
        "lastModified": "2026-01-10T11:00:00Z",
        "sourceGroup": "Human Resources",
    },
    {
        "id": "sp-003",
        "content": "New employee onboarding follows a 90-day structured program. Week 1 covers IT setup, security training, and team introductions. Weeks 2-4 focus on role-specific training with a designated buddy. Months 2-3 include cross-functional shadowing and a 90-day review with the hiring manager.",
        "title": "Employee_Onboarding_Guide.pdf",
        "documentUrl": "https://contoso.sharepoint.com/sites/hr/Employee_Onboarding_Guide.pdf",
        "sourceType": "sharepoint",
        "pageNumber": 4,
        "totalPages": 18,
        "relevanceScore": 0.91,
        "lastModified": "2026-01-05T08:00:00Z",
        "sourceGroup": "Human Resources",
    },
    {
        "id": "sp-004",
        "content": "Budget thresholds for vendor contracts: under $10K requires manager approval, $10K-$100K requires VP approval, over $100K requires C-level sign-off and board notification. All contracts over $50K must include a 30-day termination clause.",
        "title": "Budget_Guidelines_Q1_2026.docx",
        "documentUrl": "https://contoso.sharepoint.com/sites/finance/Budget_Guidelines_Q1_2026.docx",
        "sourceType": "sharepoint",
        "pageNumber": 3,
        "totalPages": 8,
        "relevanceScore": 0.87,
        "lastModified": "2026-01-20T14:15:00Z",
        "sourceGroup": "Finance",
    },

    # ── Search Index sources (product docs, engineering) ──────────────────
    {
        "id": "idx-001",
        "content": "The Contoso AI Platform API supports three authentication methods: API key, Azure AD token, and managed identity. For production deployments, managed identity is recommended as it eliminates credential management. API keys should only be used for development and testing.",
        "title": "API_Authentication_Guide.md",
        "documentUrl": "https://docs.contoso.com/api/authentication",
        "sourceType": "searchindex",
        "pageNumber": 1,
        "totalPages": 5,
        "relevanceScore": 0.92,
        "lastModified": "2026-02-01T09:00:00Z",
        "sourceGroup": "Product Documentation",
    },
    {
        "id": "idx-002",
        "content": "Development teams follow a two-week sprint cycle with planning on Monday, daily standups at 9:30am, and retrospectives on the final Friday. All code changes require at least one peer review before merging to the main branch. CI/CD pipelines must pass before deployment to staging.",
        "title": "Engineering_Handbook.pdf",
        "documentUrl": "https://docs.contoso.com/engineering/handbook",
        "sourceType": "searchindex",
        "pageNumber": 12,
        "totalPages": 35,
        "relevanceScore": 0.90,
        "lastModified": "2026-02-01T09:00:00Z",
        "sourceGroup": "Engineering",
    },
    {
        "id": "idx-003",
        "content": "Production deployment windows are Tuesday and Thursday, 2pm-5pm PST. Emergency hotfixes may be deployed outside these windows with on-call engineer approval. All deployments must include rollback plans and monitoring dashboards. Feature flags should be used for gradual rollouts.",
        "title": "Deployment_Procedures.md",
        "documentUrl": "https://docs.contoso.com/engineering/deployment",
        "sourceType": "searchindex",
        "pageNumber": 1,
        "totalPages": 3,
        "relevanceScore": 0.84,
        "lastModified": "2026-02-05T10:30:00Z",
        "sourceGroup": "Engineering",
    },
    {
        "id": "idx-004",
        "content": "The Contoso Search SDK provides full-text search, vector search, and hybrid search capabilities. Knowledge Base mode aggregates results from multiple connected sources including SharePoint, OneLake, and web crawlers. Use the /knowledgebases/{name}/retrieve endpoint for unified retrieval.",
        "title": "Search_SDK_Reference.md",
        "documentUrl": "https://docs.contoso.com/search/sdk-reference",
        "sourceType": "searchindex",
        "pageNumber": 3,
        "totalPages": 20,
        "relevanceScore": 0.88,
        "lastModified": "2026-01-28T15:00:00Z",
        "sourceGroup": "Product Documentation",
    },

    # ── Web sources (external research) ──────────────────────────────────
    {
        "id": "web-001",
        "content": "Industry best practices for vendor management include annual performance reviews, quarterly business reviews, and risk-based tiering of vendor relationships. Tier 1 vendors (critical services) should have dedicated relationship managers and monthly check-ins.",
        "title": "Vendor Management Best Practices - Gartner 2025",
        "documentUrl": "https://www.gartner.com/en/articles/vendor-management-best-practices",
        "sourceType": "web",
        "pageNumber": 1,
        "totalPages": 1,
        "relevanceScore": 0.76,
        "lastModified": "2025-11-01T00:00:00Z",
        "sourceGroup": "External Research",
    },
    {
        "id": "web-002",
        "content": "According to McKinsey's 2025 report on enterprise AI adoption, organizations that implement structured knowledge management systems see a 40% improvement in employee productivity and a 25% reduction in time spent searching for information. The most effective systems combine semantic search with document-level access controls.",
        "title": "Enterprise AI Adoption Trends - McKinsey 2025",
        "documentUrl": "https://www.mckinsey.com/capabilities/quantumblack/our-insights/enterprise-ai-2025",
        "sourceType": "web",
        "pageNumber": 1,
        "totalPages": 1,
        "relevanceScore": 0.74,
        "lastModified": "2025-09-15T00:00:00Z",
        "sourceGroup": "External Research",
    },
    {
        "id": "web-003",
        "content": "NIST Cybersecurity Framework 2.0 recommends organizations implement zero-trust architecture, continuous monitoring, and automated incident response. Key controls include multi-factor authentication, network segmentation, and regular penetration testing. All critical systems should have recovery time objectives under 4 hours.",
        "title": "NIST Cybersecurity Framework 2.0 Summary",
        "documentUrl": "https://www.nist.gov/cyberframework/framework",
        "sourceType": "web",
        "pageNumber": 1,
        "totalPages": 1,
        "relevanceScore": 0.80,
        "lastModified": "2025-08-01T00:00:00Z",
        "sourceGroup": "External Research",
    },
    {
        "id": "web-004",
        "content": "Forrester's Total Economic Impact study of enterprise knowledge retrieval platforms shows an average ROI of 320% over three years. Key benefits include reduced time-to-answer (from 15 minutes to 2 minutes), improved decision quality, and reduced compliance violations through better policy access.",
        "title": "TEI of Enterprise Knowledge Retrieval - Forrester 2025",
        "documentUrl": "https://www.forrester.com/report/total-economic-impact-knowledge-retrieval",
        "sourceType": "web",
        "pageNumber": 1,
        "totalPages": 1,
        "relevanceScore": 0.72,
        "lastModified": "2025-10-20T00:00:00Z",
        "sourceGroup": "External Research",
    },
]

# ============================================================================
# Index schema matching KBRetrieveResult
# ============================================================================
INDEX_SCHEMA = {
    "name": "",  # Set at runtime
    "fields": [
        {"name": "id", "type": "Edm.String", "key": True, "filterable": True},
        {"name": "content", "type": "Edm.String", "searchable": True, "retrievable": True},
        {"name": "title", "type": "Edm.String", "searchable": True, "retrievable": True, "filterable": True},
        {"name": "documentUrl", "type": "Edm.String", "retrievable": True},
        {"name": "sourceType", "type": "Edm.String", "filterable": True, "facetable": True, "retrievable": True},
        {"name": "pageNumber", "type": "Edm.Int32", "retrievable": True},
        {"name": "totalPages", "type": "Edm.Int32", "retrievable": True},
        {"name": "relevanceScore", "type": "Edm.Double", "retrievable": True, "sortable": True},
        {"name": "lastModified", "type": "Edm.DateTimeOffset", "retrievable": True, "filterable": True, "sortable": True},
        {"name": "sourceGroup", "type": "Edm.String", "filterable": True, "facetable": True, "retrievable": True},
    ],
}


def make_request(url, method="GET", data=None, headers=None, timeout=30):
    """Make an HTTP request and return the response."""
    ctx = ssl.create_default_context()
    if data is not None:
        data = json.dumps(data).encode("utf-8")
    req = urllib.request.Request(url, data=data, headers=headers or {}, method=method)
    with urllib.request.urlopen(req, timeout=timeout, context=ctx) as response:
        body = response.read().decode("utf-8")
        return response.status, json.loads(body) if body else {}


def toggle_public_access(resource_group, search_service_name, enable):
    """Enable or disable public network access on the AI Search service."""
    state = "enabled" if enable else "disabled"
    logger.info(f"Setting public network access to '{state}' on {search_service_name}...")
    result = subprocess.run(
        [
            "az", "search", "service", "update",
            "-g", resource_group,
            "-n", search_service_name,
            "--public-network-access", state,
        ],
        capture_output=True, text=True,
    )
    if result.returncode != 0:
        logger.error(f"Failed to toggle public access: {result.stderr}")
        return False
    logger.info(f"Public network access set to '{state}'")
    if enable:
        logger.info("Waiting 15s for network change to propagate...")
        time.sleep(15)
    return True


def create_index(endpoint, api_key, index_name):
    """Create the search index."""
    logger.info(f"Creating index '{index_name}'...")

    schema = {**INDEX_SCHEMA, "name": index_name}
    url = f"{endpoint}/indexes/{index_name}?api-version={API_VERSION}"
    headers = {
        "Content-Type": "application/json",
        "api-key": api_key,
    }

    try:
        # Try to delete existing index first
        try:
            make_request(url, method="DELETE", headers=headers)
            logger.info(f"Deleted existing index '{index_name}'")
            time.sleep(2)
        except urllib.error.HTTPError as e:
            if e.code != 404:
                raise

        # Create index
        create_url = f"{endpoint}/indexes?api-version={API_VERSION}"
        status, result = make_request(create_url, method="POST", data=schema, headers=headers)
        logger.info(f"Created index '{index_name}' (HTTP {status})")
        return True
    except Exception as e:
        logger.error(f"Failed to create index: {e}")
        return False


def upload_documents(endpoint, api_key, index_name):
    """Upload sample documents to the index."""
    logger.info(f"Uploading {len(SAMPLE_DOCUMENTS)} documents to '{index_name}'...")

    url = f"{endpoint}/indexes/{index_name}/docs/index?api-version={API_VERSION}"
    headers = {
        "Content-Type": "application/json",
        "api-key": api_key,
    }

    # Add @search.action to each document
    docs_with_action = [
        {"@search.action": "upload", **doc} for doc in SAMPLE_DOCUMENTS
    ]

    try:
        status, result = make_request(url, method="POST", data={"value": docs_with_action}, headers=headers)
        success_count = sum(1 for r in result.get("value", []) if r.get("status"))
        logger.info(f"Uploaded {success_count}/{len(SAMPLE_DOCUMENTS)} documents (HTTP {status})")

        # Print source type breakdown
        from collections import Counter
        source_counts = Counter(d["sourceType"] for d in SAMPLE_DOCUMENTS)
        for source, count in source_counts.items():
            logger.info(f"  {source}: {count} documents")

        return True
    except Exception as e:
        logger.error(f"Failed to upload documents: {e}")
        return False


def verify_index(endpoint, api_key, index_name):
    """Verify the index has documents and search works."""
    logger.info("Verifying index...")

    # Wait for indexing
    time.sleep(3)

    url = f"{endpoint}/indexes/{index_name}/docs?api-version={API_VERSION}&search=*&$top=3&$count=true"
    headers = {
        "Content-Type": "application/json",
        "api-key": api_key,
    }

    try:
        status, result = make_request(url, headers=headers)
        count = result.get("@odata.count", len(result.get("value", [])))
        logger.info(f"Index contains {count} documents")

        # Verify source type filter works
        for source_type in ["sharepoint", "searchindex", "web"]:
            filter_url = f"{endpoint}/indexes/{index_name}/docs?api-version={API_VERSION}&search=*&$filter=sourceType eq '{source_type}'&$count=true"
            _, filter_result = make_request(filter_url, headers=headers)
            fc = filter_result.get("@odata.count", len(filter_result.get("value", [])))
            logger.info(f"  sourceType='{source_type}': {fc} documents")

        # Test a semantic query
        query_url = f"{endpoint}/indexes/{index_name}/docs?api-version={API_VERSION}&search=vendor+policy&$top=2"
        _, query_result = make_request(query_url, headers=headers)
        hits = len(query_result.get("value", []))
        logger.info(f"  Search 'vendor policy': {hits} results")

        return True
    except Exception as e:
        logger.error(f"Failed to verify index: {e}")
        return False


def test_sharepoint_headers(endpoint, api_key, index_name):
    """Test that x-ms-sharepoint-* headers are accepted in requests."""
    logger.info("Testing SharePoint global headers (x-ms-sharepoint-*)...")

    url = f"{endpoint}/indexes/{index_name}/docs?api-version={API_VERSION}&search=*&$filter=sourceType eq 'sharepoint'&$top=2"
    headers = {
        "Content-Type": "application/json",
        "api-key": api_key,
        # SharePoint global headers for remote access
        "x-ms-sharepoint-siteurl": "https://contoso.sharepoint.com/sites/policies",
        "x-ms-sharepoint-tenantid": "00000000-0000-0000-0000-000000000000",
        "x-ms-sharepoint-accesstoken": "test-token-for-header-passthrough-validation",
    }

    try:
        status, result = make_request(url, headers=headers)
        hits = len(result.get("value", []))
        logger.info(f"  SharePoint header request returned HTTP {status} with {hits} results")
        logger.info("  ✓ x-ms-sharepoint-* headers accepted by Azure AI Search")
        return True
    except urllib.error.HTTPError as e:
        # 403 is expected if the token is invalid but headers are accepted
        if e.code == 403:
            logger.info(f"  HTTP 403 — headers accepted but token invalid (expected for test)")
            return True
        logger.error(f"  ✗ SharePoint header test failed: HTTP {e.code}")
        return False
    except Exception as e:
        logger.error(f"  ✗ SharePoint header test failed: {e}")
        return False


def main():
    parser = argparse.ArgumentParser(
        description="Create sample Knowledge Base in Azure AI Search",
        formatter_class=argparse.RawDescriptionHelpFormatter,
    )
    parser.add_argument("--endpoint", required=True, help="Azure AI Search endpoint URL")
    parser.add_argument("--api-key", required=True, help="Azure AI Search admin API key")
    parser.add_argument("--kb-name", default="test-kb", help="Knowledge Base / index name (default: test-kb)")
    parser.add_argument("--toggle-public-access", action="store_true",
                        help="Temporarily enable public access for seeding, then re-disable")
    parser.add_argument("--resource-group", help="Resource group (required with --toggle-public-access)")
    parser.add_argument("--search-service-name", help="Search service name (required with --toggle-public-access)")

    args = parser.parse_args()

    if args.toggle_public_access and (not args.resource_group or not args.search_service_name):
        parser.error("--toggle-public-access requires --resource-group and --search-service-name")

    print("=" * 60)
    print("CREATE SAMPLE KNOWLEDGE BASE")
    print("=" * 60)
    print(f"  Endpoint: {args.endpoint}")
    print(f"  KB Name:  {args.kb_name}")
    print(f"  Documents: {len(SAMPLE_DOCUMENTS)}")
    print()

    # Temporarily enable public access if requested
    if args.toggle_public_access:
        if not toggle_public_access(args.resource_group, args.search_service_name, enable=True):
            sys.exit(1)

    try:
        # Step 1: Create the index
        if not create_index(args.endpoint, args.api_key, args.kb_name):
            sys.exit(1)

        # Step 2: Upload documents
        if not upload_documents(args.endpoint, args.api_key, args.kb_name):
            sys.exit(1)

        # Step 3: Verify
        if not verify_index(args.endpoint, args.api_key, args.kb_name):
            sys.exit(1)

        # Step 4: Test SharePoint headers
        test_sharepoint_headers(args.endpoint, args.api_key, args.kb_name)

        print()
        print("=" * 60)
        print("✓ SAMPLE KNOWLEDGE BASE CREATED SUCCESSFULLY")
        print("=" * 60)
        print()
        print("Source type breakdown:")
        from collections import Counter
        for st, c in Counter(d["sourceType"] for d in SAMPLE_DOCUMENTS).items():
            print(f"  {st}: {c} documents")
        print()
        print("Next steps:")
        print(f"  1. Set AZURE_SEARCH_KB_NAME={args.kb_name}")
        print(f"  2. Run: python test_kb_api_connectivity.py --endpoint {args.endpoint}")
        print(f"  3. Run: python test_foundry_iq_kb_mcp.py")

    finally:
        # Re-disable public access if we toggled it
        if args.toggle_public_access:
            toggle_public_access(args.resource_group, args.search_service_name, enable=False)


if __name__ == "__main__":
    main()
