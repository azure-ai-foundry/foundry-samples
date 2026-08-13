#!/usr/bin/env python3
"""Render a run-scoped summary from validation-pilot result artifacts."""

from __future__ import annotations

import argparse
import json
import re
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any
from urllib.parse import quote

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
SECTION_ORDER = ("sample failure", "infrastructure/error", "skipped/not-completed", "passed")
SECTION_LABELS = {
    "sample failure": "Sample failures",
    "infrastructure/error": "Infrastructure/errors",
    "skipped/not-completed": "Skipped/not-completed",
}
DIAGNOSTIC_LIMIT = 240
SUPPORTED_SCHEMA_VERSIONS = {1, 2}
LEGACY_COMPLETED_STAGES = {
    "L3 validation": "build readiness validation",
    "L3 validation invocation": "build readiness invocation",
    "L4 validation": "live-service validation",
    "L4 validation invocation": "live-service validation invocation",
}
DIAGNOSTIC_PATTERNS = (
    re.compile(r"(?:\berror\b|^FAIL:|^ERROR:|^SKIP:|^runner error:)", re.IGNORECASE),
)
VERDICT_PATTERN = re.compile(r"^verdict=", re.IGNORECASE)
REPOSITORY_PATTERN = re.compile(r"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$")
SHA_PATTERN = re.compile(r"^[0-9a-f]{7,40}$")


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
        or payload.get("schema_version") not in SUPPORTED_SCHEMA_VERSIONS
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
    if (
        not isinstance(value, dict)
        or set(value) != REQUIRED
        or value.get("schema_version") not in SUPPORTED_SCHEMA_VERSIONS
    ):
        raise ContractError("result must be a supported schema object")
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
    completed_stage = LEGACY_COMPLETED_STAGES.get(
        value["completed_stage"], value["completed_stage"]
    )
    return {
        **value,
        "completed_stage": completed_stage,
        "completed_at": timestamp(value["completed_at"], "completed_at"),
        "diagnostic_path": diagnostic,
    }


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


def markdown_cell(value: Any) -> str:
    return (
        str(value)
        .replace("\\", "\\\\")
        .replace("|", "\\|")
        .replace("`", "'")
        .replace("\r", " ")
        .replace("\n", " ")
    )


def sample_url(record: dict[str, Any]) -> str | None:
    run = record.get("run", {})
    repository = run.get("repository")
    sha = run.get("sha")
    path = record["sample"]["path"]
    if (
        not isinstance(repository, str)
        or not REPOSITORY_PATTERN.fullmatch(repository)
        or not isinstance(sha, str)
        or not SHA_PATTERN.fullmatch(sha)
    ):
        return None
    return f"https://github.com/{repository}/tree/{sha}/{quote(path, safe='/')}"


def diagnostic_excerpt(record: dict[str, Any]) -> str:
    if record.get("error"):
        value = record["error"]
    else:
        try:
            lines = [
                line.strip()
                for line in record["diagnostic_path"]
                .read_text(encoding="utf-8", errors="replace")
                .splitlines()
                if line.strip() and not line.lstrip().startswith("$ ")
            ]
        except OSError:
            return "No diagnostic excerpt available"
        value = next(
            (
                line
                for line in lines
                if any(pattern.search(line) for pattern in DIAGNOSTIC_PATTERNS)
            ),
            next(
                (line for line in reversed(lines) if VERDICT_PATTERN.search(line)),
                lines[0] if lines else "",
            ),
        )
    value = re.sub(
        r"(?i)(token|secret|password|api[_ -]?key)(\s*[:=]\s*)\S+",
        r"\1\2[redacted]",
        value,
    )
    value = re.sub(r"[\x00-\x1f\x7f]", " ", value)
    value = " ".join(value.split())
    if len(value) > DIAGNOSTIC_LIMIT:
        value = value[: DIAGNOSTIC_LIMIT - 1].rstrip() + "…"
    return value or "No diagnostic excerpt available"


def render_sample(record: dict[str, Any]) -> str:
    path = markdown_cell(record["sample"]["path"])
    url = sample_url(record)
    return f"[`{path}`]({url})" if url else f"`{path}`"


def render_rows(records: list[dict[str, Any]], outcome: str) -> list[str]:
    rows = []
    for record in records:
        sample = render_sample(record)
        stage = markdown_cell(record["completed_stage"])
        if outcome == "passed":
            rows.append(f"| {sample} | {stage} | {record['duration_seconds']}s |")
            continue
        reason = markdown_cell(diagnostic_excerpt(record))
        if outcome == "skipped/not-completed":
            next_action = "Add validator support or retain explicit skip"
        elif outcome == "infrastructure/error":
            next_action = "Inspect workflow job"
        else:
            next_action = "Inspect sample validation output"
        rows.append(f"| {sample} | {stage} | `{reason}` | {next_action} |")
    return rows


def render(records: list[dict[str, Any]], run_url: str | None, complete: bool) -> str:
    counts = {
        outcome: sum(record["outcome"] == outcome for record in records)
        for outcome in OUTCOMES
    }
    action_required = counts["sample failure"] + counts["infrastructure/error"]
    lines = ["## Validation report", ""]
    run = next((record.get("run") for record in records if record.get("run")), {})
    if run:
        metadata = f"Run {run.get('run_id')} · attempt {run.get('run_attempt')}"
        repository = run.get("repository")
        sha = run.get("sha")
        if (
            isinstance(repository, str)
            and REPOSITORY_PATTERN.fullmatch(repository)
            and isinstance(sha, str)
            and SHA_PATTERN.fullmatch(sha)
        ):
            metadata += (
                f" · validated SHA [`{sha[:12]}`]"
                f"(https://github.com/{repository}/commit/{sha})"
            )
        if run_url:
            metadata += f" · [Workflow run]({run_url})"
        lines.extend([f"_{metadata}_", ""])
    lines.extend(
        [
            f"> **Action required:** {action_required} record(s) need maintainer attention",
            f"> **Informational:** {counts['skipped/not-completed']} record(s) are intentionally skipped",
            f"> **Fleet {'complete' if complete else 'incomplete'}:** {len(records)} result record(s) reported",
            "",
            f"**{len(records)} total · {counts['passed']} passed · "
            f"{counts['sample failure']} sample failures · "
            f"{counts['infrastructure/error']} infrastructure/errors · "
            f"{counts['skipped/not-completed']} skipped/not-completed**",
            "",
        ]
    )
    for outcome in SECTION_ORDER[:3]:
        section_records = sorted(
            (record for record in records if record["outcome"] == outcome),
            key=lambda record: (record["sample"]["language"], record["sample"]["path"]),
        )
        if not section_records:
            continue
        lines.extend(
            [
                f"### {OUTCOMES[outcome].split(' ', 1)[0]} {SECTION_LABELS[outcome]} ({len(section_records)})",
                "",
                "| Sample | Stage | Reason | Next action |",
                "|---|---|---|---|",
                *render_rows(section_records, outcome),
                "",
            ]
        )
    passed = sorted(
        (record for record in records if record["outcome"] == "passed"),
        key=lambda record: (record["sample"]["language"], record["sample"]["path"]),
    )
    if passed:
        lines.extend(
            [
                f"<details><summary>{OUTCOMES['passed']} ({len(passed)})</summary>",
                "",
                "| Sample | Stage | Duration |",
                "|---|---|---:|",
                *render_rows(passed, "passed"),
                "",
                "</details>",
                "",
            ]
        )
    lines.extend(
        [
            f"**Legend:** {OUTCOMES['passed']} · {OUTCOMES['sample failure']} · "
            f"{OUTCOMES['infrastructure/error']} · {OUTCOMES['skipped/not-completed']}",
            "",
            "Diagnostics are summarized and sanitized. Full logs remain available from "
            "the workflow run, subject to GitHub Actions retention and authentication.",
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
        records, incomplete = collect(args.results_dir, load_expected(args.expected_samples))
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(
            render(records, args.run_url, not incomplete),
            encoding="utf-8",
            newline="\n",
        )
    except (ContractError, OSError) as exc:
        print(f"render-validation-report: {exc}", file=sys.stderr)
        return 1
    if incomplete:
        print("render-validation-report: incomplete result handoff", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
