# Azure Function — Calculator API with Private Storage
#
# A minimal Azure Function that demonstrates VNet Integration by:
#   1. Performing arithmetic (works without VNet)
#   2. Storing results in a private Azure Blob Storage account
#      (requires VNet Integration — storage has public access disabled)
#
# This is the key "Azure Function behind a VNet" scenario:
#   - The function itself is publicly accessible (publicNetworkAccess: Enabled)
#   - But its OUTBOUND traffic goes through VNet Integration
#   - This lets it reach a private storage account that has no public endpoint
#
# Endpoints:
#   POST /api/calculate           - Compute and store result in private blob
#   GET  /api/history             - Read calculation history from private blob
#   GET  /api/healthz             - Health check including private storage connectivity
#
# Run locally: func start  (needs Azurite or a real storage connection string)
# Deploy:      func azure functionapp publish <APP_NAME>

import json
import logging
import os
from datetime import datetime, timezone

import azure.functions as func

app = func.FunctionApp(http_auth_level=func.AuthLevel.ANONYMOUS)

# Container name for calculation history
HISTORY_CONTAINER = "calculation-history"


def _get_blob_service_client():
    """Get a BlobServiceClient using the Function App's storage connection.

    In VNet mode, this connection goes through VNet Integration to reach
    the private storage account. Without VNet Integration, the connection
    would fail because the storage account has public access disabled.
    """
    from azure.storage.blob import BlobServiceClient

    # Use the Function App's own storage account (already configured via
    # AzureWebJobsStorage). In production you might use a separate account.
    conn_str = os.environ.get("AzureWebJobsStorage", "")
    if not conn_str:
        return None
    return BlobServiceClient.from_connection_string(conn_str)


def _ensure_container(blob_service_client):
    """Create the history container if it doesn't exist."""
    try:
        container = blob_service_client.get_container_client(HISTORY_CONTAINER)
        if not container.exists():
            container.create_container()
        return container
    except Exception as e:
        logging.warning(f"Could not ensure container: {e}")
        return None


def _store_result(operation, a, b, result):
    """Store a calculation result in private blob storage.

    This is the VNet Integration proof point: the blob write goes through
    the VNet to reach a storage account with public access disabled.
    Returns a dict with storage status info.
    """
    try:
        client = _get_blob_service_client()
        if client is None:
            return {"stored": False, "reason": "No storage connection configured"}

        container = _ensure_container(client)
        if container is None:
            return {"stored": False, "reason": "Could not access storage container"}

        # Write result as a timestamped blob
        timestamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S")
        blob_name = f"{timestamp}_{operation}_{a}_{b}.json"
        record = {
            "timestamp": timestamp,
            "operation": operation,
            "a": a,
            "b": b,
            "result": result,
        }
        container.upload_blob(blob_name, json.dumps(record), overwrite=True)

        return {"stored": True, "blob": blob_name}

    except Exception as e:
        return {"stored": False, "reason": str(e)}


@app.route(route="calculate", methods=["POST"])
def calculate(req: func.HttpRequest) -> func.HttpResponse:
    """Perform an arithmetic operation and store the result in private storage.

    This endpoint demonstrates VNet Integration: the computation itself doesn't
    need VNet access, but storing the result in private blob storage does.
    The response includes a 'storage' field showing whether the private
    storage write succeeded (proving VNet Integration works).
    """
    logging.info("Calculate function invoked")

    try:
        body = req.get_json()
    except ValueError:
        return func.HttpResponse(
            json.dumps({"error": "Invalid JSON body"}),
            status_code=400,
            mimetype="application/json",
        )

    operation = body.get("operation")
    a = body.get("a")
    b = body.get("b")

    if operation is None or a is None or b is None:
        return func.HttpResponse(
            json.dumps({"error": "Missing required fields: operation, a, b"}),
            status_code=400,
            mimetype="application/json",
        )

    try:
        a = float(a)
        b = float(b)
    except (TypeError, ValueError):
        return func.HttpResponse(
            json.dumps({"error": "a and b must be numbers"}),
            status_code=400,
            mimetype="application/json",
        )

    if operation == "add":
        result = a + b
    elif operation == "subtract":
        result = a - b
    elif operation == "multiply":
        result = a * b
    elif operation == "divide":
        if b == 0:
            return func.HttpResponse(
                json.dumps({"error": "Division by zero"}),
                status_code=400,
                mimetype="application/json",
            )
        result = a / b
    else:
        return func.HttpResponse(
            json.dumps({"error": f"Unknown operation: {operation}"}),
            status_code=400,
            mimetype="application/json",
        )

    # Store result in private blob storage (VNet Integration proof point)
    storage_status = _store_result(operation, a, b, result)

    return func.HttpResponse(
        json.dumps(
            {
                "operation": operation,
                "a": a,
                "b": b,
                "result": result,
                "storage": storage_status,
            }
        ),
        status_code=200,
        mimetype="application/json",
    )


@app.route(route="history", methods=["GET"])
def history(req: func.HttpRequest) -> func.HttpResponse:
    """Read recent calculation history from private blob storage.

    Returns the last N calculations stored by the calculate endpoint.
    This proves VNet Integration works for both read and write to private storage.
    """
    try:
        limit = int(req.params.get("limit", "10"))
    except ValueError:
        limit = 10

    try:
        client = _get_blob_service_client()
        if client is None:
            return func.HttpResponse(
                json.dumps(
                    {"error": "No storage connection", "vnet_integration": False}
                ),
                status_code=503,
                mimetype="application/json",
            )

        container = client.get_container_client(HISTORY_CONTAINER)
        if not container.exists():
            return func.HttpResponse(
                json.dumps({"history": [], "count": 0, "vnet_integration": True}),
                status_code=200,
                mimetype="application/json",
            )

        # List blobs in reverse chronological order (newest first)
        blobs = sorted(
            container.list_blobs(), key=lambda b: b.name, reverse=True
        )[:limit]

        records = []
        for blob in blobs:
            data = container.download_blob(blob.name).readall()
            records.append(json.loads(data))

        return func.HttpResponse(
            json.dumps(
                {
                    "history": records,
                    "count": len(records),
                    "vnet_integration": True,
                }
            ),
            status_code=200,
            mimetype="application/json",
        )

    except Exception as e:
        return func.HttpResponse(
            json.dumps(
                {
                    "error": f"Storage access failed: {e}",
                    "vnet_integration": False,
                }
            ),
            status_code=503,
            mimetype="application/json",
        )


@app.route(route="healthz", methods=["GET"])
def healthz(req: func.HttpRequest) -> func.HttpResponse:
    """Health check including private storage connectivity.

    Reports whether the function can reach its private storage account,
    which is the key indicator that VNet Integration is working.
    """
    storage_ok = False
    storage_detail = "not checked"

    try:
        client = _get_blob_service_client()
        if client is not None:
            # Try to access storage — this goes through VNet Integration
            props = client.get_account_information()
            storage_ok = True
            storage_detail = "connected via VNet Integration"
        else:
            storage_detail = "no connection string configured"
    except Exception as e:
        storage_detail = f"unreachable: {e}"

    return func.HttpResponse(
        json.dumps(
            {
                "status": "ok" if storage_ok else "degraded",
                "compute": "ok",
                "private_storage": {
                    "reachable": storage_ok,
                    "detail": storage_detail,
                },
            }
        ),
        status_code=200,
        mimetype="application/json",
    )
