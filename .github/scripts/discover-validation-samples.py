#!/usr/bin/env python3
"""Discover the full sample inventory and emit deterministic validation matrices."""

from __future__ import annotations

import argparse
import json
import re
from pathlib import Path

SUPPORTED_LANGUAGES = {
    "csharp": "csharp",
    "java": "java",
    "python": "python",
    "typescript": "typescript",
    "javascript": "typescript",
}


def sample_id(path: str) -> str:
    return path.removeprefix("samples/").replace("/", "-")


def declares_live_service_validation(metadata: Path) -> bool:
    for line in metadata.read_text(encoding="utf-8").splitlines():
        without_comment = line.split("#", 1)[0].rstrip()
        if re.match(r"^[ \t]*l4[ \t]*:", without_comment):
            raise ValueError(
                f"{metadata}: legacy top-level key 'l4' is unsupported; "
                "rename it to 'live_service_validation'"
            )
        if re.match(
            r"^[ \t]*live_service_validation[ \t]*:", without_comment
        ):
            return True
    return False


def discover(root: Path) -> dict:
    samples = []
    for metadata in sorted(root.glob("samples/**/sample.yaml")):
        path = metadata.parent.relative_to(root).as_posix()
        language = path.split("/")[1]
        validator_language = SUPPORTED_LANGUAGES.get(language)
        live_service_validation_declared = declares_live_service_validation(metadata)
        sample = {
            "id": sample_id(path),
            "path": path,
            "language": language,
            "shape": "full-fleet",
        }
        samples.append(sample)
        sample["validator_language"] = validator_language or ""
        sample["eligible"] = validator_language is not None
        sample["skip_reason"] = (
            "" if validator_language else f"language '{language}' is not supported by build readiness"
        )
        sample["live_service_validation_declared"] = live_service_validation_declared

    identities = [
        {key: sample[key] for key in ("id", "path", "language", "shape")}
        for sample in samples
    ]
    return {
        "schema_version": 2,
        "samples": identities,
        "validation": {
            sample["id"]: {
                key: sample[key]
                for key in (
                    "validator_language",
                    "eligible",
                    "skip_reason",
                    "live_service_validation_declared",
                )
            }
            for sample in samples
        },
        "matrix": samples,
    }


def write_matrix(path: Path, samples: list[dict]) -> None:
    path.write_text(
        json.dumps({"include": samples}, separators=(",", ":")),
        encoding="utf-8",
    )


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, default=Path("."))
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--matrix", type=Path, required=True)
    parser.add_argument("--build-readiness-matrix", type=Path)
    parser.add_argument("--live-service-matrix", type=Path)
    args = parser.parse_args()

    payload = discover(args.root.resolve())
    args.manifest.write_text(
        json.dumps(
            {"schema_version": payload["schema_version"], "samples": payload["samples"], "validation": payload["validation"]},
            indent=2,
            sort_keys=True,
        )
        + "\n",
        encoding="utf-8",
    )
    write_matrix(args.matrix, payload["matrix"])
    if args.build_readiness_matrix:
        write_matrix(
            args.build_readiness_matrix,
            [
                sample
                for sample in payload["matrix"]
                if not sample["live_service_validation_declared"]
            ],
        )
    if args.live_service_matrix:
        write_matrix(
            args.live_service_matrix,
            [
                sample
                for sample in payload["matrix"]
                if sample["live_service_validation_declared"]
            ],
        )
    print(json.dumps({"count": len(payload["matrix"]), "matrix": payload["matrix"]}, separators=(",", ":")))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
