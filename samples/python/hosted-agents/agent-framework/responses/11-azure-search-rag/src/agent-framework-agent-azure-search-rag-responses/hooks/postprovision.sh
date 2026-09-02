#!/usr/bin/env sh
# azd postprovision hook (POSIX / sh).
#
# Creates and seeds the sample index after the search service exists.

set -e

# Run beside provision_index.py regardless of azd's working directory.
cd "$(CDPATH= cd "$(dirname "$0")/.." && pwd)"

if [ -z "$AZURE_SEARCH_ENDPOINT" ]; then
  echo "AZURE_SEARCH_ENDPOINT is not set. Provide an existing Azure AI Search service endpoint." >&2
  exit 1
fi

if [ -z "$AZURE_SEARCH_INDEX_NAME" ]; then
  AZURE_SEARCH_INDEX_NAME="contoso-outdoors"
  export AZURE_SEARCH_INDEX_NAME
  azd env set AZURE_SEARCH_INDEX_NAME "$AZURE_SEARCH_INDEX_NAME"
fi

echo "Provisioning the Azure AI Search index '$AZURE_SEARCH_INDEX_NAME'..."
python -m pip install -q azure-search-documents azure-identity python-dotenv
python provision_index.py
