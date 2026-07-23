# Copyright (c) Microsoft. All rights reserved.

"""Probe package. Importing it registers every built-in probe as a side effect,
so the runner only needs ``import probes`` to discover them all.

To add a probe, drop a module in this package and import it below (or rely on a
star of the built-ins). See ``DEVELOPING_PROBES.md`` at the sample root.
"""

from __future__ import annotations

# Import each probe module for its @register side effects. Order here does not
# matter — run order is controlled by each probe's ``order`` attribute.
from . import system  # noqa: F401  container.info, env.dump
from . import host  # noqa: F401  dns.* + conn.* per host
from . import direct  # noqa: F401  conn.direct
from . import egress  # noqa: F401  egress.public
from . import net_counters  # noqa: F401  net.udp_counters
