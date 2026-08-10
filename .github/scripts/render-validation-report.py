#!/usr/bin/env python3
"""Render a run-scoped summary from validation-pilot v1 result artifacts."""

from __future__ import annotations

import argparse
import json
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

OUTCOMES = {
    "passed": "✅ Passed",
    "sample failure": "❌ Sample failure",
    "infrastructure/error": "⚠️ Infrastructure/error",
    "skipped/not-completed": "⏭️ Skipped/not-completed",
}
REQUIRED = {
    "schema_version", "sample", "outcome", "completed_stage", "duration_seconds",
    "diagnostic_reference", "artifact_reference", "completed_at", "run",
}
RUN_FIELDS = {"repository", "workflow", "run_id", "run_attempt", "sha", "ref", "started_at"}


class ContractError(ValueError):
    pass


def load_json(path: Path, label: str) -> Any:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except FileNotFoundError as exc:
        raise ContractError(f"{label} not found: {path}") from exc
    except json.JSONDecodeError as exc:
        raise ContractError(f"{label} is not valid JSON: {exc}") from exc


def timestamp(value: Any, field: str) -> datetime:
    if not isinstance(value, str) or not value.endswith("Z"):
        raise ContractError(f"{field} must be an ISO-8601 UTC timestamp ending in Z")
    try:
        parsed = datetime.fromisoformat(value[:-1] + "+00:00")
    except ValueError as exc:
        raise ContractError(f"{field} is not a valid timestamp") from exc
    if parsed.utcoffset() != timezone.utc.utcoffset(parsed):
        raise ContractError(f"{field} must be UTC")
    return parsed


def sample_identity(value: Any, field: str) -> dict[str, str]:
    keys = {"id", "path", "language", "shape"}
    if not isinstance(value, dict) or set(value) != keys:
        raise ContractError(f"{field} must contain exactly id, path, language, and shape")
    if any(not isinstance(value[key], str) or not value[key] for key in keys):
        raise ContractError(f"{field} fields must be non-empty strings")
    if not value["path"].startswith("samples/") or ".." in Path(value["path"]).parts:
        raise ContractError(f"{field}.path must be a safe repository-relative samples/ path")
    return {key: value[key] for key in keys}


def load_expected(path: Path) -> list[dict[str, str]]:
    payload = load_json(path, "sample manifest")
    if (
        not isinstance(payload, dict)
        or payload.get("schema_version") != 1
        or not isinstance(payload.get("samples"), list)
        or not payload["samples"]
    ):
        raise ContractError("sample manifest must contain a non-empty samples array")
    samples = [sample_identity(value, "manifest sample") for value in payload["samples"]]
    ids = [value["id"] for value in samples]
    if ids != sorted(set(ids)):
        raise ContractError("manifest samples must be sorted and unique by id")
    return samples


def load_record(path: Path, expected: dict[str, str]) -> dict[str, Any]:
    value = load_json(path, f"result artifact {path}")
    if not isinstance(value, dict) or set(value) != REQUIRED or value.get("schema_version") != 1:
        raise ContractError("result must be a schema_version 1 object")
    missing = REQUIRED - value.keys()
    if missing:
        raise ContractError(f"result is missing fields: {sorted(missing)}")
    sample = sample_identity(value["sample"], "result sample")
    if sample != expected:
        raise ContractError(f"sample identity does not match manifest: {sample['id']}")
    if value["outcome"] not in OUTCOMES:
        raise ContractError(f"unsupported outcome: {value['outcome']!r}")
    if not isinstance(value["completed_stage"], str) or not value["completed_stage"]:
        raise ContractError("completed_stage must be non-empty")
    if not isinstance(value["duration_seconds"], (int, float)) or isinstance(value["duration_seconds"], bool) or value["duration_seconds"] < 0:
        raise ContractError("duration_seconds must be non-negative")
    timestamp(value["completed_at"], "completed_at")
    run = value["run"]
    if not isinstance(run, dict) or set(run) != RUN_FIELDS:
        raise ContractError(f"run is missing fields: {sorted(RUN_FIELDS - set(run or {}))}")
    timestamp(run["started_at"], "run.started_at")
    for field in ("diagnostic_reference", "artifact_reference"):
        reference = value[field]
        if (
            not isinstance(reference, str)
            or not reference
            or Path(reference).is_absolute()
            or ".." in Path(reference).parts
            or len(Path(reference).parts) != 1
        ):
            raise ContractError(f"{field} must be a relative filename")
    diagnostic = path.parent / value["diagnostic_reference"]
    if not diagnostic.is_file():
        raise ContractError(f"missing diagnostic: {diagnostic}")
    return {**value, "completed_at": timestamp(value["completed_at"], "completed_at")}


def collect(results_dir: Path, expected: list[dict[str, str]]) -> tuple[list[dict[str, Any]], bool]:
    if not results_dir.is_dir():
        raise ContractError(f"result artifact directory not found: {results_dir}")
    expected_by_id = {value["id"]: value for value in expected}
    records: dict[str, dict[str, Any]] = {}
    incomplete = False
    for path in sorted(results_dir.glob("*/sample-result.json")):
        try:
            raw = load_json(path, f"result artifact {path}")
            sample_id = raw.get("sample", {}).get("id") if isinstance(raw, dict) else None
            if sample_id not in expected_by_id:
                raise ContractError(f"unexpected sample id: {sample_id}")
            if sample_id in records:
                raise ContractError(f"duplicate result artifact for {sample_id}")
            record = load_record(path, expected_by_id[sample_id])
            records[sample_id] = record
        except ContractError as exc:
            incomplete = True
            records[f"invalid:{path}"] = {
                "sample": {"id": path.name, "path": f"<invalid artifact: {path.name}>", "language": "reporting", "shape": "error"},
                "outcome": "infrastructure/error", "completed_stage": "reporting",
                "duration_seconds": 0, "completed_at": None,
                "diagnostic_reference": "—", "artifact_reference": path.name, "run": {},
                "error": str(exc),
            }
    for sample in expected:
        if sample["id"] not in records:
            incomplete = True
            records[f"missing:{sample['id']}"] = {
                "sample": sample, "outcome": "infrastructure/error",
                "completed_stage": "reporting", "duration_seconds": 0,
                "completed_at": None, "diagnostic_reference": "—",
                "artifact_reference": "—", "run": {},
                "error": f"expected result artifact is missing for {sample['id']}",
            }
    return sorted(records.values(), key=lambda value: value["sample"]["path"]), incomplete


def render(records: list[dict[str, Any]], run_url: str | None) -> str:
    lines = [
        "## Validation report", "",
        "_Run-scoped summary; only attempted samples are listed._", "",
        "| Sample | Outcome | Completed stage | Duration | Last run (UTC) | Diagnostic/artifact |",
        "|---|---|---|---:|---|---|",
    ]
    for record in records:
        completed = record["completed_at"].strftime("%Y-%m-%d %H:%M:%S UTC") if record["completed_at"] else "—"
        sample = f"`{record['sample']['path']}`"
        evidence = f"`{record['diagnostic_reference']}` / `{record['artifact_reference']}`"
        lines.append(f"| {sample} | {OUTCOMES[record['outcome']]} | {record['completed_stage']} | {record['duration_seconds']}s | {completed} | {evidence} |")
        if record.get("error"):
            lines.append(f"| {sample} | ⚠️ Incomplete | reporting | — | — | {record['error']} |")
    if run_url:
        lines.extend(["", f"Run evidence: {run_url}"])
    lines.extend(["", "**Legend:** ✅ passed · ❌ sample failure · ⚠️ infrastructure/error · ⏭️ skipped/not-completed", ""])
    return "\n".join(lines)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--results-dir", type=Path, required=True)
    parser.add_argument("--expected-samples", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--run-url")
    args = parser.parse_args()
    try:
        records, incomplete = collect(args.results_dir, load_expected(args.expected_samples))
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(render(records, args.run_url), encoding="utf-8", newline="\n")
    except (ContractError, OSError) as exc:
        print(f"render-validation-report: {exc}", file=sys.stderr)
        return 1
    if incomplete:
        print("render-validation-report: incomplete result handoff", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
