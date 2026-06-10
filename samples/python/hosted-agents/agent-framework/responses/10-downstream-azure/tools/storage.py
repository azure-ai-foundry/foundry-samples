# Copyright (c) Microsoft. All rights reserved.

"""Azure Blob Storage tools.

RBAC: the calling principal must have ``Storage Blob Data Contributor`` (or a
narrower equivalent) on the target container — see the sample README.
"""

import logging
import os
from typing import Annotated

from agent_framework import tool
from azure.core.exceptions import ResourceExistsError, ResourceNotFoundError
from azure.identity import DefaultAzureCredential
from azure.storage.blob import BlobServiceClient, ContainerClient
from pydantic import Field

logger = logging.getLogger(__name__)


def _container_client() -> ContainerClient:
    account = os.environ["AZURE_STORAGE_ACCOUNT_NAME"]
    container = os.environ["AZURE_STORAGE_CONTAINER_NAME"]
    service = BlobServiceClient(
        account_url=f"https://{account}.blob.core.windows.net",
        credential=DefaultAzureCredential(),
    )
    return service.get_container_client(container)


@tool(approval_mode="never_require")
def storage_put_blob(
    name: Annotated[str, Field(description="Blob name (acts as the key).")],
    content: Annotated[str, Field(description="Blob content as text.")],
) -> str:
    """Upsert a blob in the configured Azure Storage container."""
    container = _container_client()
    try:
        container.upload_blob(name=name, data=content.encode("utf-8"), overwrite=True)
    except ResourceNotFoundError:
        try:
            container.create_container()
        except ResourceExistsError:
            pass
        container.upload_blob(name=name, data=content.encode("utf-8"), overwrite=True)
    logger.info("Uploaded blob %s (%d bytes)", name, len(content))
    return f"Uploaded blob '{name}'."


@tool(approval_mode="never_require")
def storage_get_blob(
    name: Annotated[str, Field(description="Blob name to read.")],
) -> str:
    """Read a blob's content as text."""
    container = _container_client()
    try:
        return container.download_blob(name).readall().decode("utf-8")
    except ResourceNotFoundError:
        return f"No blob named '{name}'."
