# Copyright (c) Microsoft. All rights reserved.

"""Tool functions for the downstream-Azure sample.

Each module wraps one Azure data-plane SDK with a small set of generic CRUD-ish
tools. All tools build their SDK client with ``DefaultAzureCredential()`` so
that, in the hosted sandbox, calls authenticate as the agent's per-agent
Microsoft Entra identity.
"""

from .servicebus import (
    servicebus_peek_messages,
    servicebus_send_message,
)
from .storage import (
    storage_get_blob,
    storage_put_blob,
)

ALL_TOOLS = [
    storage_put_blob,
    storage_get_blob,
    servicebus_send_message,
    servicebus_peek_messages,
]
