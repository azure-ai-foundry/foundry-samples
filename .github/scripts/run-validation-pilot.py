#!/usr/bin/env python3
"""Run one curated sample and write its canonical normalized result."""

from __future__ import annotations

import argparse
import json
import os
import subprocess
import sys
import time
from datetime import datetime, timezone
from pathlib import Path

OUTCOMES = {
    0: "passed",
    1: "sample failure",
    2: "infrastructure/error",
}


def utc_now() -> str:
    return datetime.now(timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--sample-id", required=True)
    parser.add_argument("--language", required=True)
    parser.add_argument("--shape", required=True)
    parser.add_argument("--sample-path", required=True)
    parser.add_argument("--validator", default=".github/scripts/validate-sample.sh")
    parser.add_argument("--bash", default="bash")
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--diagnostic", type=Path, required=True)
    parser.add_argument("--run-id", default=os.environ.get("GITHUB_RUN_ID", "local"))
    parser.add_argument("--run-attempt", default=os.environ.get("GITHUB_RUN_ATTEMPT", "1"))
    parser.add_argument("--sha", default=os.environ.get("GITHUB_SHA", "local"))
    parser.add_argument("--ref", default=os.environ.get("GITHUB_REF", "local"))
    parser.add_argument("--repository", default=os.environ.get("GITHUB_REPOSITORY", "local"))
    parser.add_argument("--workflow", default=os.environ.get("GITHUB_WORKFLOW", "validation pilot"))
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    started_at = utc_now()
    started = time.monotonic()
    command = [
        args.bash,
        args.validator,
        "--level",
        "3",
        "--language",
        args.language,
        "--sample-dir",
        args.sample_path,
    ]
    try:
        completed = subprocess.run(command, capture_output=True, text=True)
        diagnostic = (
            "$ " + " ".join(command) + "\n"
            + completed.stdout
            + completed.stderr
        )
        outcome = OUTCOMES.get(completed.returncode, "infrastructure/error")
        stage = "L3 validation" if completed.returncode in OUTCOMES else "L3 validation invocation"
    except (OSError, subprocess.SubprocessError) as exc:
        completed = None
        diagnostic = "$ " + " ".join(command) + f"\nrunner error: {exc}\n"
        outcome = "infrastructure/error"
        stage = "L3 validation invocation"

    completed_at = utc_now()
    result = {
        "schema_version": 1,
        "sample": {
            "id": args.sample_id,
            "path": args.sample_path,
            "language": args.language,
            "shape": args.shape,
        },
        "outcome": outcome,
        "completed_stage": stage,
        "duration_seconds": round(time.monotonic() - started, 3),
        "diagnostic_reference": args.diagnostic.name,
        "artifact_reference": args.output.name,
        "completed_at": completed_at,
        "run": {
            "repository": args.repository,
            "workflow": args.workflow,
            "run_id": args.run_id,
            "run_attempt": args.run_attempt,
            "sha": args.sha,
            "ref": args.ref,
            "started_at": started_at,
        },
    }
    args.diagnostic.parent.mkdir(parents=True, exist_ok=True)
    args.diagnostic.write_text(diagnostic, encoding="utf-8")
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(f"{args.sample_id}: {outcome} ({result['duration_seconds']}s)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
