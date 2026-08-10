#!/usr/bin/env python3
"""Render a run-scoped GitHub Actions summary from normalized result artifacts.

The artifact shape consumed here is an explicit adapter boundary for the reporting
pilot. The validation session owns the canonical producer schema; this consumer
must be aligned to that schema before production use.
"""

from __future__ import annotations

import argparse
import json
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any
from urllib.parse import quote, urlparse


OUTCOMES = {
    "passed": "✅ Passed",
    "sample_failure": "❌ Sample failure",
    "infrastructure_error": "⚠️ Infrastructure/error",
    "skipped": "⏭️ Skipped/not-completed",
}
REQUIRED_FIELDS = {
    "sample",
    "outcome",
    "stage",
    "duration_seconds",
    "completed_at",
}


class ContractError(ValueError):
    """Raised when the reporting adapter input is incomplete or malformed."""


def load_json(path: Path, label: str) -> Any:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except FileNotFoundError as exc:
        raise ContractError(f"{label} not found: {path}") from exc
    except json.JSONDecodeError as exc:
        raise ContractError(f"{label} is not valid JSON: {exc}") from exc


def validate_sample(value: Any, field: str = "sample") -> str:
    if not isinstance(value, str) or not value.startswith("samples/"):
        raise ContractError(f"{field} must be a repository-relative samples/ path")
    if ".." in Path(value).parts or any(c in value for c in "|\r\n"):
        raise ContractError(f"{field} contains an unsafe path")
    return Path(value).as_posix()


def validate_url(value: Any, field: str) -> str | None:
    if value is None:
        return None
    if not isinstance(value, str):
        raise ContractError(f"{field} must be a string")
    parsed = urlparse(value)
    if parsed.scheme not in ("http", "https") or not parsed.netloc:
        raise ContractError(f"{field} must be an absolute HTTP(S) URL")
    return value


def parse_timestamp(value: Any, field: str) -> datetime:
    if not isinstance(value, str) or not value.endswith("Z"):
        raise ContractError(f"{field} must be an ISO-8601 UTC timestamp ending in Z")
    try:
        parsed = datetime.fromisoformat(value[:-1] + "+00:00")
    except ValueError as exc:
        raise ContractError(f"{field} is not a valid timestamp") from exc
    if parsed.utcoffset() != timezone.utc.utcoffset(parsed):
        raise ContractError(f"{field} must be UTC")
    return parsed


def load_expected(path: Path) -> list[str]:
    value = load_json(path, "expected samples")
    if not isinstance(value, list) or not value:
        raise ContractError("expected samples must be a non-empty JSON array")
    samples = [validate_sample(item, "expected sample") for item in value]
    if samples != sorted(set(samples)):
        raise ContractError("expected samples must be sorted and unique")
    return samples


def load_record(path: Path) -> dict[str, Any]:
    value = load_json(path, f"result artifact {path.name}")
    if not isinstance(value, dict):
        raise ContractError(f"result artifact {path.name} must be a JSON object")
    if value.get("schema_version") != 1:
        raise ContractError(f"result artifact {path.name} must use schema_version 1")
    missing = REQUIRED_FIELDS - value.keys()
    if missing:
        raise ContractError(
            f"result artifact {path.name} is missing fields: {sorted(missing)}"
        )
    sample = validate_sample(value["sample"])
    outcome = value["outcome"]
    if outcome not in OUTCOMES:
        raise ContractError(f"{sample}.outcome is unsupported: {outcome!r}")
    if not isinstance(value["stage"], str) or not value["stage"]:
        raise ContractError(f"{sample}.stage must be a non-empty string")
    if (
        not isinstance(value["duration_seconds"], (int, float))
        or isinstance(value["duration_seconds"], bool)
        or value["duration_seconds"] < 0
    ):
        raise ContractError(f"{sample}.duration_seconds must be non-negative")
    completed_at = parse_timestamp(value["completed_at"], f"{sample}.completed_at")
    diagnostic_url = validate_url(value.get("diagnostic_url"), f"{sample}.diagnostic_url")
    artifact_url = validate_url(value.get("artifact_url"), f"{sample}.artifact_url")
    return {
        "sample": sample,
        "outcome": outcome,
        "stage": value["stage"],
        "duration_seconds": value["duration_seconds"],
        "completed_at": completed_at,
        "diagnostic_url": diagnostic_url,
        "artifact_url": artifact_url,
    }


def collect_records(results_dir: Path, expected: list[str]) -> tuple[list[dict[str, Any]], bool]:
    if not results_dir.is_dir():
        raise ContractError(f"result artifact directory not found: {results_dir}")
    records: dict[str, dict[str, Any]] = {}
    incomplete = False
    for path in sorted(results_dir.glob("*.json")):
        try:
            record = load_record(path)
        except ContractError as exc:
            incomplete = True
            records[f"<invalid:{path.name}>"] = {
                "sample": f"<invalid artifact: {path.name}>",
                "outcome": "infrastructure_error",
                "stage": "reporting",
                "duration_seconds": 0,
                "completed_at": None,
                "diagnostic_url": None,
                "artifact_url": None,
                "error": str(exc),
            }
            continue
        if record["sample"] in records:
            incomplete = True
            record["error"] = f"duplicate result artifact for {record['sample']}"
            record["outcome"] = "infrastructure_error"
        records[record["sample"]] = record

    for sample in expected:
        if sample not in records:
            incomplete = True
            records[sample] = {
                "sample": sample,
                "outcome": "infrastructure_error",
                "stage": "reporting",
                "duration_seconds": 0,
                "completed_at": None,
                "diagnostic_url": None,
                "artifact_url": None,
                "error": "expected result artifact is missing",
            }
    return sorted(records.values(), key=lambda record: record["sample"]), incomplete


def link(value: str | None) -> str:
    if not value:
        return "—"
    encoded = quote(value, safe=":/?#@!$&'*+,;=%._~-")
    return f"[link]({encoded})"


def render(records: list[dict[str, Any]], run_url: str | None) -> str:
    lines = [
        "## Validation report",
        "",
        "_Run-scoped summary; only attempted samples are listed._",
        "",
        "| Sample | Outcome | Completed stage | Duration | Last run (UTC) | Diagnostic/artifact |",
        "|---|---|---|---:|---|---|",
    ]
    for record in records:
        completed = (
            record["completed_at"].strftime("%Y-%m-%d %H:%M:%S UTC")
            if record["completed_at"]
            else "—"
        )
        evidence = link(record["diagnostic_url"] or record["artifact_url"])
        sample = f"`{record['sample']}`"
        lines.append(
            f"| {sample} | {OUTCOMES[record['outcome']]} | {record['stage']} | "
            f"{record['duration_seconds']}s | {completed} | {evidence} |"
        )
        if record.get("error"):
            lines.append(f"| `{record['sample']}` | ⚠️ Incomplete | reporting | — | — | {record['error']} |")
    if run_url:
        lines.extend(["", f"Run evidence: {link(run_url)}"])
    lines.extend(
        [
            "",
            "**Legend:** ✅ passed · ❌ sample failure · ⚠️ infrastructure/error · "
            "⏭️ skipped/not-completed",
            "",
        ]
    )
    return "\n".join(lines)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--results-dir", type=Path, required=True)
    parser.add_argument("--expected-samples", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--run-url")
    args = parser.parse_args()
    try:
        expected = load_expected(args.expected_samples)
        records, incomplete = collect_records(args.results_dir, expected)
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
