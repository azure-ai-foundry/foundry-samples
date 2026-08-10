#!/usr/bin/env python3
"""Fail closed when a pilot run did not persist one complete result per attempt."""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

REQUIRED_OUTCOMES = {"passed", "sample failure", "infrastructure/error", "skipped/not-completed"}
REQUIRED_FIELDS = {
    "schema_version", "sample", "outcome", "completed_stage", "duration_seconds",
    "diagnostic_reference", "artifact_reference", "completed_at", "run",
}


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--artifacts", type=Path, required=True)
    args = parser.parse_args()
    manifest = json.loads(args.manifest.read_text(encoding="utf-8"))
    expected = {sample["id"]: sample for sample in manifest["samples"]}
    found = {}
    errors = []
    for result_path in sorted(args.artifacts.glob("*/sample-result.json")):
        try:
            result = json.loads(result_path.read_text(encoding="utf-8"))
            missing = REQUIRED_FIELDS - result.keys()
            sample = result["sample"]
            if missing or result["schema_version"] != 1 or result["outcome"] not in REQUIRED_OUTCOMES:
                raise ValueError(f"invalid schema or outcome (missing={sorted(missing)})")
            sample_id = sample["id"]
            if sample_id not in expected or sample_id in found:
                raise ValueError(f"unexpected or duplicate sample id: {sample_id}")
            if sample != expected[sample_id] and any(
                sample.get(key) != expected[sample_id].get(key)
                for key in ("id", "path", "language", "shape")
            ):
                raise ValueError("sample identity does not match manifest")
            diagnostic = result_path.parent / result["diagnostic_reference"]
            if not diagnostic.is_file():
                raise ValueError(f"missing diagnostic: {diagnostic}")
            found[sample_id] = result
        except (OSError, KeyError, TypeError, ValueError, json.JSONDecodeError) as exc:
            errors.append(f"{result_path}: {exc}")
    missing_ids = sorted(set(expected) - set(found))
    if missing_ids:
        errors.append("missing result artifacts: " + ", ".join(missing_ids))
    if errors:
        for error in errors:
            print(f"ERROR: {error}", file=sys.stderr)
        return 1
    output = args.artifacts / "run-summary.json"
    output.write_text(json.dumps({"schema_version": 1, "results": found}, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(f"validated {len(found)} complete sample results")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
