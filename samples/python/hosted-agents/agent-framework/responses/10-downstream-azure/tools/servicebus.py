# Copyright (c) Microsoft. All rights reserved.

"""Azure Service Bus tools (queue, namespace-MI auth).

RBAC: the calling principal needs ``Azure Service Bus Data Sender`` to send
and ``Azure Service Bus Data Receiver`` to peek/receive — see the sample
README.
"""

import logging
import os
from typing import Annotated

from agent_framework import tool
from azure.identity import DefaultAzureCredential
from azure.servicebus import ServiceBusClient, ServiceBusMessage
from pydantic import Field

logger = logging.getLogger(__name__)


def _client() -> ServiceBusClient:
    fqdn = os.environ["AZURE_SERVICEBUS_FQDN"]
    return ServiceBusClient(
        fully_qualified_namespace=fqdn,
        credential=DefaultAzureCredential(),
    )


def _queue_name() -> str:
    return os.environ["AZURE_SERVICEBUS_QUEUE_NAME"]


@tool(approval_mode="never_require")
def servicebus_send_message(
    body: Annotated[str, Field(description="Message body as text.")],
) -> str:
    """Send a single message to the configured Service Bus queue."""
    queue = _queue_name()
    with _client() as client, client.get_queue_sender(queue) as sender:
        sender.send_messages(ServiceBusMessage(body))
    logger.info("Sent message to %s (%d bytes)", queue, len(body))
    return f"Sent message to queue '{queue}'."


@tool(approval_mode="never_require")
def servicebus_peek_messages(
    max_count: Annotated[
        int,
        Field(description="Maximum number of messages to peek.", ge=1, le=50),
    ] = 10,
) -> str:
    """Peek up to ``max_count`` messages from the queue without removing them."""
    queue = _queue_name()
    with _client() as client, client.get_queue_receiver(queue) as receiver:
        msgs = receiver.peek_messages(max_message_count=max_count)
    if not msgs:
        return "Queue is empty."
    bodies = [str(m) for m in msgs]
    return f"Peeked {len(bodies)} message(s):\n" + "\n".join(f"- {b}" for b in bodies)
