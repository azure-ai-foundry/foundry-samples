# Copyright (c) Microsoft. All rights reserved.

"""Allow running the package as ``python -m hello_world_a365_agent``."""

from .main import main

if __name__ == "__main__":
    raise SystemExit(main())
