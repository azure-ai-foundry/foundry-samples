#!/usr/bin/env python3
"""Run the configured dashboard pilot samples and emit normalized results."""

from __future__ import annotations

import argparse
import json
import os
import re
import subprocess
import sys
from copy import deepcopy
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from validation_health_state import StateError, extract_state


LANGUAGES = {
    "csharp": "csharp",
    "python": "python",
    "typescript": "typescript",
    "javascript": "typescript",
    "java": "java",
    "go": "go",
}
SHA_PATTERN = re.compile(r"^[0-9a-f]{40}$")


class PilotError(ValueError):
    """Raised when the pilot cannot produce a trustworthy result document."""


def load_config(path: Path, repo_root: Path) -> list[str]:
    try:
        config = json.loads(path.read_text(encoding="utf-8"))
    except FileNotFoundError as exc:
        raise PilotError(f"pilot config not found: {path}") from exc
    except json.JSONDecodeError as exc:
        raise PilotError(f"pilot config is not valid JSON: {exc}") from exc
    if not isinstance(config, dict) or config.get("schema_version") != 1:
        raise PilotError("pilot config must be a schema_version 1 JSON object")
    samples = config.get("samples")
    if not isinstance(samples, list) or not samples:
        raise PilotError("pilot config samples must be a non-empty list")
    if any(not isinstance(sample, str) for sample in samples):
        raise PilotError("pilot config sample paths must be strings")
    if samples != sorted(set(samples)):
        raise PilotError("pilot config sample paths must be sorted and unique")
    for sample in samples:
        if not sample.startswith("samples/") or not (repo_root / sample).is_dir():
            raise PilotError(f"configured sample directory does not exist: {sample}")
        language_dir = sample.split("/", 2)[1]
        if language_dir not in LANGUAGES:
            raise PilotError(f"configured sample language is unsupported: {sample}")
    return samples


def load_previous_results(path: Path | None, samples: set[str]) -> dict[str, Any]:
    if path is None:
        return {}
    try:
        body = path.read_text(encoding="utf-8")
    except FileNotFoundError as exc:
        raise PilotError(f"previous dashboard body not found: {path}") from exc
    try:
        payload = extract_state(body)
    except StateError as exc:
        raise PilotError(str(exc)) from exc
    if payload is None:
        return {}
    if payload.get("schema_version") != 1 or not isinstance(payload.get("results"), dict):
        raise PilotError("previous dashboard hidden state has an unsupported contract")
    return {
        sample: deepcopy(payload["results"][sample])
        for sample in samples
        if sample in payload["results"]
    }


def resolve_sha(repo_root: Path, argument: str | None) -> str:
    if argument is not None:
        if not SHA_PATTERN.fullmatch(argument):
            raise PilotError("--source-sha must be a full lowercase Git SHA")
        return argument
    try:
        sha = subprocess.run(
            ["git", "rev-parse", "HEAD"],
            cwd=repo_root,
            check=True,
            capture_output=True,
            text=True,
        ).stdout.strip()
    except (OSError, subprocess.CalledProcessError) as exc:
        raise PilotError("could not resolve source SHA") from exc
    if not SHA_PATTERN.fullmatch(sha):
        raise PilotError("resolved source SHA is invalid")
    return sha


def probe_l4(yq: list[str], repo_root: Path, sample: str) -> bool:
    yaml_path = repo_root / sample / "sample.yaml"
    completed = subprocess.run(
        [*yq, "eval", 'has("l4")', yaml_path.as_posix()],
        cwd=repo_root,
        capture_output=True,
        text=True,
    )
    if completed.returncode != 0:
        sys.stdout.write(completed.stdout)
        sys.stderr.write(completed.stderr)
        raise PilotError(f"could not inspect L4 declaration: {sample}")
    value = completed.stdout.strip()
    if value not in ("true", "false"):
        raise PilotError(f"unexpected L4 declaration probe for {sample}: {value!r}")
    return value == "true"


def run_validator(
    bash: str,
    validator: Path,
    repo_root: Path,
    sample: str,
    level: str,
) -> int:
    try:
        validator_argument = validator.relative_to(repo_root).as_posix()
    except ValueError:
        validator_argument = validator.as_posix()
    command = [bash, validator_argument, "--level", level, "--sample-dir", sample]
    if level == "3":
        language_dir = sample.split("/", 2)[1]
        command.extend(["--language", LANGUAGES[language_dir]])
    completed = subprocess.run(
        command,
        cwd=repo_root,
        capture_output=True,
        text=True,
        env=os.environ.copy(),
    )
    print(f"===== {sample} L{level} (exit={completed.returncode}) =====")
    sys.stdout.write(completed.stdout)
    sys.stderr.write(completed.stderr)
    return completed.returncode


def status_for_exit_code(exit_code: int) -> str:
    if exit_code == 0:
        return "pass"
    if exit_code == 1:
        return "failure"
    return "error"


def result_record(status: str, run_at: str, evidence_url: str | None) -> dict[str, str]:
    record = {"status": status, "run_at": run_at}
    if evidence_url:
        record["evidence_url"] = evidence_url
    return record


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--config",
        type=Path,
        default=Path(".github/validation-health-pilot.json"),
    )
    parser.add_argument("--repo-root", type=Path, default=Path("."))
    parser.add_argument(
        "--validator",
        type=Path,
        default=Path(".github/scripts/validate-sample.sh"),
    )
    parser.add_argument("--bash", default="bash")
    parser.add_argument("--yq", nargs="+", default=["yq"])
    parser.add_argument("--output", type=Path)
    parser.add_argument("--previous-body", type=Path)
    parser.add_argument("--source-sha")
    parser.add_argument("--evidence-url")
    parser.add_argument("--run-at", help="fixed ISO-8601 UTC timestamp for tests")
    parser.add_argument("--detect-l4-only", action="store_true")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    repo_root = args.repo_root.resolve()
    try:
        samples = load_config(args.config, repo_root)
        declarations = {
            sample: probe_l4(args.yq, repo_root, sample) for sample in samples
        }
        if args.detect_l4_only:
            print("true" if any(declarations.values()) else "false")
            return 0
        if args.output is None:
            raise PilotError("--output is required unless --detect-l4-only is used")
        if os.environ.get("SKIP_PROVISION") != "false":
            raise PilotError("SKIP_PROVISION must be exactly false for the pilot run")

        source_sha = resolve_sha(repo_root, args.source_sha)
        run_at = args.run_at or datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
        if not re.fullmatch(r"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z", run_at):
            raise PilotError("--run-at must use YYYY-MM-DDTHH:MM:SSZ")

        results = load_previous_results(args.previous_body, set(samples))
        overall_failed = False
        validator = (repo_root / args.validator).resolve()
        if not validator.is_file():
            raise PilotError(f"validator not found: {validator}")

        for sample in samples:
            sample_result = results.setdefault(sample, {})
            if not isinstance(sample_result, dict):
                raise PilotError(f"previous result for {sample} is malformed")

            l3_exit = run_validator(args.bash, validator, repo_root, sample, "3")
            l3_status = status_for_exit_code(l3_exit)
            sample_result["l3"] = result_record(
                l3_status, run_at, args.evidence_url
            )
            if l3_exit != 0:
                overall_failed = True

            if not declarations[sample]:
                sample_result.pop("l4", None)
            elif l3_exit == 0:
                l4_exit = run_validator(args.bash, validator, repo_root, sample, "4")
                l4_status = status_for_exit_code(l4_exit)
                sample_result["l4"] = result_record(
                    l4_status, run_at, args.evidence_url
                )
                if l4_exit != 0:
                    overall_failed = True

        payload = {
            "schema_version": 1,
            "source_sha": source_sha,
            "results": results,
        }
        args.output.parent.mkdir(parents=True, exist_ok=True)
        temporary = Path(f"{args.output}.tmp")
        temporary.write_text(
            json.dumps(payload, indent=2, sort_keys=True) + "\n",
            encoding="utf-8",
            newline="\n",
        )
        temporary.replace(args.output)
    except (PilotError, OSError) as exc:
        print(f"run-validation-health-pilot: {exc}", file=sys.stderr)
        return 2
    return 1 if overall_failed else 0


if __name__ == "__main__":
    raise SystemExit(main())
