# Copyright (c) Microsoft. All rights reserved.

"""Provision the Foundry IQ knowledge base used by this sample.

Runs once, before you deploy the agent. It creates (or updates) four things in
your Azure AI Search service:

  1. A search index (``foundry-iq-index``) with a semantic configuration.
  2. Seed documents (the "Earth at night" corpus) uploaded into that index.
  3. A knowledge source over the index.
  4. A knowledge base that orchestrates the knowledge source and synthesizes
     answers with an Azure OpenAI model.

The knowledge base exposes an MCP endpoint
(``{search}/knowledgebases/{kb}/mcp``) with a single ``knowledge_base_retrieve``
tool. The hosted agent reaches that endpoint through a Foundry toolbox.

Usage (from this directory, with the venv activated and ``az login`` done):

    python provision_kb.py

Required env vars (also read from a local ``.env`` file if present):

    AZURE_SEARCH_ENDPOINT          e.g. https://<your-search>.search.windows.net
    AZURE_OPENAI_ENDPOINT          e.g. https://<account>.openai.azure.com
    AZURE_AI_MODEL_DEPLOYMENT_NAME e.g. gpt-5.4-mini

Optional env vars (sensible defaults shown):

    AZURE_SEARCH_INDEX_NAME    foundry-iq-index
    KNOWLEDGE_SOURCE_NAME      foundry-iq-ks
    KNOWLEDGE_BASE_NAME        foundry-iq-kb
    AZURE_AI_MODEL_NAME        (defaults to AZURE_AI_MODEL_DEPLOYMENT_NAME)

Your identity needs ``Search Service Contributor`` (to create the index and the
knowledge source/base) and ``Search Index Data Contributor`` (to upload
documents) on the search service.
"""

import json
import os
import shutil
import subprocess
import sys

import requests
from azure.identity import DefaultAzureCredential
from dotenv import load_dotenv

SEARCH_SCOPE = "https://search.azure.com/.default"
API_VERSION = "2026-05-01-preview"
SEMANTIC_CONFIG_NAME = "default-semantic-config"

DOCUMENTS: list[dict[str, str]] = [
    {
        "id": "1",
        "title": "City lights from space",
        "content": (
            "At night, Earth's most populated regions glow with artificial light. "
            "The brightest clusters trace major cities and coastlines, while large "
            "dark areas correspond to oceans, deserts, and sparsely populated "
            "regions. Satellite sensors such as the VIIRS Day/Night Band capture "
            "these patterns with remarkable detail."
        ),
    },
    {
        "id": "2",
        "title": "Aurora and natural light",
        "content": (
            "Not all light seen at night is artificial. Auroras near the poles, "
            "lightning in storm systems, and moonlight reflecting off clouds all "
            "contribute to the planet's nighttime glow. Wildfires and gas flares "
            "also appear as bright points in nighttime imagery."
        ),
    },
    {
        "id": "3",
        "title": "Measuring light pollution",
        "content": (
            "Nighttime satellite imagery is used to study light pollution, urban "
            "growth, and energy use. Researchers compare brightness over time to "
            "track economic activity, the spread of electrification, and the impact "
            "of power outages following natural disasters."
        ),
    },
    {
        "id": "4",
        "title": "Shipping lanes and fishing fleets",
        "content": (
            "Lights at sea reveal human activity far from land. Concentrations of "
            "light over the open ocean often come from fishing fleets that use bright "
            "lamps to attract catch. Oil platforms and busy shipping lanes are also "
            "visible in nighttime imagery."
        ),
    },
]


def _require(name: str) -> str:
    value = os.environ.get(name, "").strip()
    if not value:
        print(f"ERROR: required environment variable '{name}' is not set.", file=sys.stderr)
        sys.exit(1)
    return value


class SearchClient:
    def __init__(self, endpoint: str, token: str) -> None:
        self._endpoint = endpoint.rstrip("/")
        self._headers = {
            "Authorization": f"Bearer {token}",
            "Content-Type": "application/json",
        }

    def put(self, path: str, body: dict) -> None:
        url = f"{self._endpoint}/{path}?api-version={API_VERSION}"
        response = requests.put(url, headers=self._headers, json=body, timeout=120)
        if response.status_code not in (200, 201, 204):
            print(f"ERROR: PUT {path} failed ({response.status_code}): {response.text}", file=sys.stderr)
            sys.exit(1)

    def post(self, path: str, body: dict) -> dict:
        url = f"{self._endpoint}/{path}?api-version={API_VERSION}"
        response = requests.post(url, headers=self._headers, json=body, timeout=120)
        if response.status_code not in (200, 201):
            print(f"ERROR: POST {path} failed ({response.status_code}): {response.text}", file=sys.stderr)
            sys.exit(1)
        return response.json() if response.content else {}


def create_index(client: SearchClient, index_name: str) -> None:
    print(f"Creating index '{index_name}'...")
    body = {
        "name": index_name,
        "fields": [
            {"name": "id", "type": "Edm.String", "key": True, "filterable": True},
            {"name": "title", "type": "Edm.String", "searchable": True, "retrievable": True},
            {"name": "content", "type": "Edm.String", "searchable": True, "retrievable": True},
        ],
        "semantic": {
            "configurations": [
                {
                    "name": SEMANTIC_CONFIG_NAME,
                    "prioritizedFields": {
                        "titleField": {"fieldName": "title"},
                        "prioritizedContentFields": [{"fieldName": "content"}],
                    },
                }
            ]
        },
    }
    client.put(f"indexes/{index_name}", body)


def upload_documents(client: SearchClient, index_name: str) -> None:
    print(f"Uploading {len(DOCUMENTS)} document(s) to '{index_name}'...")
    actions = [{"@search.action": "mergeOrUpload", **doc} for doc in DOCUMENTS]
    client.post(f"indexes/{index_name}/docs/index", {"value": actions})


def create_knowledge_source(client: SearchClient, ks_name: str, index_name: str) -> None:
    print(f"Creating knowledge source '{ks_name}'...")
    body = {
        "name": ks_name,
        "kind": "searchIndex",
        "searchIndexParameters": {
            "searchIndexName": index_name,
            "semanticConfigurationName": SEMANTIC_CONFIG_NAME,
            "sourceDataFields": [{"name": "title"}, {"name": "content"}],
            "searchFields": [],
        },
    }
    client.put(f"knowledgesources/{ks_name}", body)


def create_knowledge_base(client: SearchClient, kb_name: str, ks_name: str) -> None:
    print(f"Creating knowledge base '{kb_name}'...")
    aoai_endpoint = _require("AZURE_OPENAI_ENDPOINT").rstrip("/")
    deployment = _require("AZURE_AI_MODEL_DEPLOYMENT_NAME")
    model_name = os.environ.get("AZURE_AI_MODEL_NAME", "").strip() or deployment
    body = {
        "name": kb_name,
        "description": "Foundry IQ knowledge base for the hosted-agent sample.",
        "knowledgeSources": [{"name": ks_name}],
        "outputMode": "answerSynthesis",
        "retrievalReasoningEffort": {"kind": "low"},
        "models": [
            {
                "kind": "azureOpenAI",
                "azureOpenAIParameters": {
                    # Keyless: the search service managed identity has the
                    # Cognitive Services User role on the Foundry account.
                    "resourceUri": aoai_endpoint,
                    "deploymentId": deployment,
                    "modelName": model_name,
                },
            }
        ],
    }
    client.put(f"knowledgebases/{kb_name}", body)


def _set_azd_env(name: str, value: str) -> bool:
    """Best-effort: store ``value`` in the active azd environment.

    Returns ``True`` when ``azd env set`` succeeds. Falls back to ``False`` when
    azd isn't installed or there's no active environment, so the script still
    works when run standalone.
    """
    azd = shutil.which("azd")
    if not azd:
        return False
    try:
        subprocess.run([azd, "env", "set", name, value], check=True)
        return True
    except (OSError, subprocess.CalledProcessError):
        return False


def main() -> None:
    load_dotenv()

    endpoint = _require("AZURE_SEARCH_ENDPOINT")
    index_name = os.environ.get("AZURE_SEARCH_INDEX_NAME", "").strip() or "foundry-iq-index"
    ks_name = os.environ.get("KNOWLEDGE_SOURCE_NAME", "").strip() or "foundry-iq-ks"
    kb_name = os.environ.get("KNOWLEDGE_BASE_NAME", "").strip() or "foundry-iq-kb"

    token = DefaultAzureCredential().get_token(SEARCH_SCOPE).token
    client = SearchClient(endpoint, token)

    create_index(client, index_name)
    upload_documents(client, index_name)
    create_knowledge_source(client, ks_name, index_name)
    create_knowledge_base(client, kb_name, ks_name)

    mcp_endpoint = f"{endpoint.rstrip('/')}/knowledgebases/{kb_name}/mcp?api-version={API_VERSION}"
    print()
    print(f"Knowledge base '{kb_name}' is ready.")
    print(f"MCP endpoint: {mcp_endpoint}")
    print()
    if _set_azd_env("KB_MCP_ENDPOINT", mcp_endpoint):
        print("Stored the MCP endpoint as KB_MCP_ENDPOINT in your azd environment.")
    else:
        print("Next: point the toolbox connection at this MCP endpoint. Set")
        print('  azd env set KB_MCP_ENDPOINT "' + mcp_endpoint + '"')


if __name__ == "__main__":
    main()
