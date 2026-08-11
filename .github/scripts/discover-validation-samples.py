#!/usr/bin/env python3
"""Discover the full sample inventory and emit a deterministic Actions matrix."""

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


def declares_l4(metadata: Path) -> bool:
    for line in metadata.read_text(encoding="utf-8").splitlines():
        without_comment = line.split("#", 1)[0].rstrip()
        if re.match(r"^[ \t]*l4[ \t]*:", without_comment):
            return True
    return False


def discover(root: Path) -> dict:
    samples = []
    for metadata in sorted(root.glob("samples/**/sample.yaml")):
        path = metadata.parent.relative_to(root).as_posix()
        language = path.split("/")[1]
        validator_language = SUPPORTED_LANGUAGES.get(language)
        declared_l4 = declares_l4(metadata)
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
            "" if validator_language else f"language '{language}' is not supported by L3 validation"
        )
        sample["l4_declared"] = declared_l4

    identities = [
        {key: sample[key] for key in ("id", "path", "language", "shape")}
        for sample in samples
    ]
    return {
        "schema_version": 1,
        "samples": identities,
        "validation": {
            sample["id"]: {
                key: sample[key]
                for key in ("validator_language", "eligible", "skip_reason", "l4_declared")
            }
            for sample in samples
        },
        "matrix": samples,
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, default=Path("."))
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--matrix", type=Path, required=True)
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
    args.matrix.write_text(
        json.dumps({"include": payload["matrix"]}, separators=(",", ":")),
        encoding="utf-8",
    )
    print(json.dumps({"count": len(payload["matrix"]), "matrix": payload["matrix"]}, separators=(",", ":")))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
