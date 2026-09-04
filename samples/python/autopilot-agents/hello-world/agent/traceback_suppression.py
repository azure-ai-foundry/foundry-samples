"""Suppress expected outbound-HTTP tracebacks (e.g. 401s) in the logs.

Bot Framework reply failures surface as ``ClientResponseError`` and are reported
with full tracebacks/stacks by several sources: the connector client, the SDK's
default turn-error handler (which uses a raw ``print(format_exc())``), and
``aiohttp.server``. While auth is still being set up these 401s are expected
noise. This module keeps the one-line messages but drops the tracebacks, leaving
genuine code bugs (other exception types) untouched.

It is entirely self-contained: ``install_traceback_suppression(adapter)`` wires
everything up in one call, and the whole module can be deleted (along with that
call) to restore the default behaviour.
"""

from __future__ import annotations

import logging

from aiohttp import ClientResponseError
from microsoft_agents.hosting.core import TurnContext

logger = logging.getLogger(__name__)

# The Bot Framework connector client logs outbound HTTP failures (e.g. a 401 when
# replying) with ``stack_info=True`` but no exception attached. Records from this
# logger namespace only use stack_info for such error conditions, so their stacks
# are stripped wholesale as expected testing noise.
_HTTP_NOISE_LOGGER_PREFIX = "microsoft_agents.hosting.core.connector"


def install_traceback_suppression(adapter) -> None:
    """Silence expected outbound-HTTP tracebacks for ``adapter`` and the logs.

    Does two things in one call:

    * Installs a ``LogRecordFactory`` wrapper that strips tracebacks/stacks from
      expected HTTP-failure log records (see ``_scrub_http_noise``).
    * Replaces the adapter's turn-error handler with ``_on_turn_error``, which
      logs a concise line for ``ClientResponseError`` instead of the SDK
      default's raw ``print(format_exc())``.

    Call once from ``build_app`` after the adapter is created. Safe to call more
    than once; the log-factory install is guarded to run only once.
    """
    _install_log_factory()
    adapter.on_turn_error = _on_turn_error


async def _on_turn_error(context: TurnContext, error: Exception) -> None:
    """Handle uncaught turn errors without spraying tracebacks for 401s.

    Replaces the SDK default (which does ``print(format_exc())``). Outbound
    ``ClientResponseError``s -- e.g. the 401s seen while auth is still being set
    up -- are logged as a single line, since the failure is already reported by
    the connector client and re-sending would just fail again. Any other
    exception is a potential code bug, so it keeps a full traceback and the user
    still gets an error notice.
    """
    if isinstance(error, ClientResponseError):
        logger.info("Turn error sending activity: %s %s", error.status, error.message)
        return
    logger.error("Unhandled turn error", exc_info=error)
    try:
        await context.send_activity("Sorry, something went wrong handling your message.")
    except Exception:  # pragma: no cover - best-effort notice
        logger.exception("Failed to send turn-error notice")


def _install_log_factory() -> None:
    """Hide tracebacks/stacks for expected outbound HTTP failures (e.g. 401s).

    Implemented as a ``LogRecordFactory`` wrapper rather than logging filters
    because the noisy loggers (notably ``connector_client``) are imported lazily
    at request time, so they do not exist when this runs at start-up and cannot
    be located to attach a filter. The record factory runs for every record the
    moment it is created -- before any logger or handler touches it -- so it
    covers loggers and handlers created at any point in the process. Safe to call
    more than once.
    """
    if getattr(logging, "_http_noise_suppressed", False):
        return

    existing_factory = logging.getLogRecordFactory()

    def factory(*args, **kwargs):
        return _scrub_http_noise(existing_factory(*args, **kwargs))

    logging.setLogRecordFactory(factory)
    logging._http_noise_suppressed = True


def _scrub_http_noise(record: logging.LogRecord) -> logging.LogRecord:
    """Strip the traceback/stack from ``record`` if it is expected HTTP noise."""
    if _should_suppress(record):
        record.exc_info = None
        record.exc_text = None
        record.stack_info = None
    return record


def _should_suppress(record: logging.LogRecord) -> bool:
    """Decide whether a record's traceback/stack is expected HTTP noise.

    Two cases are covered:

    * Any exception chain containing a ``ClientResponseError`` (e.g. an outbound
      reply failure re-raised out of the request handler and logged by
      ``aiohttp.server``).
    * Any ERROR-level record from the connector client logger namespace. That
      library logs every HTTP failure and argument-validation error with a
      ``stack_info`` stack or ``exc_info`` traceback, none of which adds
      diagnostic value beyond the message itself.

    Genuine code bugs raise other exception types through other loggers and are
    left untouched.
    """
    if _has_http_client_error(record.exc_info):
        return True
    if record.levelno >= logging.ERROR and record.name.startswith(
        _HTTP_NOISE_LOGGER_PREFIX
    ):
        return True
    return False


def _has_http_client_error(exc_info) -> bool:
    """Return ``True`` if the exception chain contains a ``ClientResponseError``.

    Walks ``__cause__``/``__context__`` so wrapped exceptions (e.g. a reply
    failure re-raised while handling another) are still recognised.
    """
    if not exc_info or not isinstance(exc_info, tuple):
        return False
    exc = exc_info[1]
    seen: set[int] = set()
    while exc is not None and id(exc) not in seen:
        if isinstance(exc, ClientResponseError):
            return True
        seen.add(id(exc))
        exc = exc.__cause__ or exc.__context__
    return False
