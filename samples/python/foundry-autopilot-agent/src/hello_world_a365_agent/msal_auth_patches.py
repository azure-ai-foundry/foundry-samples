# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

"""Runtime monkey-patches for ``microsoft_agents.authentication.msal``.

The Managed Agent Identity Blueprint (MAIB) flow used by this sample assigns
the *blueprint* user-assigned managed identity to the Container App and then
derives per–agent-instance tokens via the AAD federated managed-identity
(``fmi_path``) mechanism. The SDK's stock
:meth:`MsalAuth.get_agentic_application_token` relies on an MSAL
``ConfidentialClientApplication`` to call the FIC token-exchange resource,
which is not how a UAMI-only deployment works.

This module replaces that method with an implementation that uses
:class:`azure.identity.aio.DefaultAzureCredential` to acquire a token for
``api://AzureADTokenExchange/.default`` scoped to the supplied
``agent_app_instance_id`` (via the ``identity_config['fmi_path']`` query
parameter that the IMDS endpoint understands).

Call :func:`apply_msal_auth_patches` once at process start-up, before any
``MsalConnectionManager`` / ``MsalAuth`` instance is constructed.
"""

from __future__ import annotations

import logging
from typing import Any, Optional

logger = logging.getLogger(__name__)

_PATCH_FLAG = "_get_agentic_application_token_patched_by_sample"


async def _get_agentic_application_token_via_default_azure_credential(
    self, tenant_id: str, agent_app_instance_id: str
) -> Optional[str]:
    """Replacement for :meth:`MsalAuth.get_agentic_application_token`.

    Acquires the agentic application token via
    :class:`azure.identity.aio.DefaultAzureCredential` instead of the stock
    MSAL ``ConfidentialClientApplication`` flow.

    The blueprint client ID (``self._msal_configuration.CLIENT_ID``) is used
    as the user-assigned managed-identity client ID on the host, and
    ``agent_app_instance_id`` is passed through to the IMDS endpoint via
    ``identity_config={"fmi_path": agent_app_instance_id}`` so the returned
    token is scoped to the specific agent application instance.
    """
    from azure.identity.aio import DefaultAzureCredential

    if not agent_app_instance_id:
        from microsoft_agents.authentication.msal.errors import (
            authentication_errors,
        )

        raise ValueError(
            str(authentication_errors.AgentApplicationInstanceIdRequired)
        )

    logger.info(
        "[patched] Acquiring agentic application token via "
        "DefaultAzureCredential for agent_app_instance_id=%s",
        agent_app_instance_id,
    )

    client_id = getattr(self._msal_configuration, "CLIENT_ID", None)

    credential_kwargs: dict[str, Any] = {
        "identity_config": {"fmi_path": agent_app_instance_id},
    }
    if client_id:
        credential_kwargs["managed_identity_client_id"] = client_id

    credential = DefaultAzureCredential(**credential_kwargs)
    try:
        access_token = await credential.get_token(
            "api://AzureADTokenExchange/.default"
        )
        return access_token.token
    except Exception:
        logger.exception(
            "Failed to acquire agentic application token via "
            "DefaultAzureCredential for agent_app_instance_id=%s",
            agent_app_instance_id,
        )
        return None
    finally:
        try:
            await credential.close()
        except Exception:
            logger.debug(
                "Ignoring error while closing DefaultAzureCredential",
                exc_info=True,
            )


def apply_msal_auth_patches() -> None:
    """Monkey-patch :class:`MsalAuth` to use ``DefaultAzureCredential``.

    Idempotent: calling this multiple times has no additional effect.
    """
    from microsoft_agents.authentication.msal.msal_auth import MsalAuth

    if getattr(MsalAuth, _PATCH_FLAG, False):
        logger.debug(
            "MsalAuth.get_agentic_application_token already patched; skipping"
        )
        return

    MsalAuth.get_agentic_application_token = (
        _get_agentic_application_token_via_default_azure_credential
    )
    setattr(MsalAuth, _PATCH_FLAG, True)

    logger.info(
        "🩹 Patched MsalAuth.get_agentic_application_token "
        "→ DefaultAzureCredential"
    )
