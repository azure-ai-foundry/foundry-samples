"""Azure Monitor configuration.

Disables the built-in Application Insights logger on import so that
FoundryCBAgent.init_tracing() does not emit duplicate telemetry.
Must be imported before the server starts.
"""

import os

os.environ["ENABLE_APPLICATION_INSIGHTS_LOGGER"] = "false"
