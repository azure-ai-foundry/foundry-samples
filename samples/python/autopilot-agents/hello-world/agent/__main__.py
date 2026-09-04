"""Run the agent with ``python -m agent``."""

from .observability import configure_observability


def main() -> None:
    """Configure telemetry before importing instrumented application code."""
    configure_observability()

    from .app import main as run_app

    run_app()


if __name__ == "__main__":
    main()
