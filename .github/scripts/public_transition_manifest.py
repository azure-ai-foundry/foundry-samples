#!/usr/bin/env python3
"""Validate and apply the public-transition path classification manifest."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
from pathlib import Path
from typing import Any, Iterable, Mapping, Sequence


SCHEMA_VERSION = 1
MATCH_SEMANTICS = "first-match"
GLOB_DIALECT = "p5-posix-v1"
CLASSIFICATIONS = frozenset(
    {"internal-only", "private-workflow-dependent", "public-ready"}
)
CHECK_CLASSES = frozenset({"none", "hosted-agents", "bicep"})
DISPOSITIONS = frozenset({"internal-only", "public-facing"})
DELEGATION_MODES = frozenset({"tracked-sample-yaml-roots", "rule-root"})
DEFAULT_CLASSIFICATION = "unresolved"
DEFAULT_CHECK_CLASS = "none"
DEFAULT_DISPOSITION = "unresolved"

_TOP_LEVEL_FIELDS = frozenset(
    {
        "schema_version",
        "match_semantics",
        "glob_dialect",
        "default_classification",
        "default_check_class",
        "default_disposition",
        "rules",
    }
)
_RULE_FIELDS = frozenset({"id", "glob", "classification", "check_class", "disposition"})
_DELEGATION_FIELDS = frozenset({"mode", "root"})
_EXTGLOB_PREFIXES = ("@(", "+(", "?(", "*(", "!(")


class ManifestError(ValueError):
    """Raised when a manifest, glob, or input path violates the v1 contract."""


def _require_exact_fields(
    value: Mapping[str, Any], expected: frozenset[str], location: str
) -> None:
    actual = frozenset(value)
    missing = sorted(expected - actual)
    extra = sorted(actual - expected)
    if missing or extra:
        details = []
        if missing:
            details.append(f"missing {missing}")
        if extra:
            details.append(f"unexpected {extra}")
        raise ManifestError(f"{location} fields are invalid: {', '.join(details)}")


def _require_nonempty_string(value: Any, location: str) -> str:
    if not isinstance(value, str) or not value or value != value.strip():
        raise ManifestError(
            f"{location} must be a non-empty string without edge whitespace"
        )
    return value


def validate_repo_path(path: Any, *, location: str = "path") -> str:
    """Return a valid repository-relative POSIX path or raise ManifestError."""

    path = _require_nonempty_string(path, location)
    if path.startswith("/") or path.startswith("./"):
        raise ManifestError(f"{location} must be repository-relative")
    if "\\" in path:
        raise ManifestError(f"{location} must use POSIX '/' separators")
    if path.endswith("/"):
        raise ManifestError(f"{location} must not end with '/'")

    segments = path.split("/")
    if any(segment == "" for segment in segments):
        raise ManifestError(f"{location} must not contain empty segments")
    if any(segment in {".", ".."} for segment in segments):
        raise ManifestError(f"{location} must not contain '.' or '..' segments")
    return path


def validate_glob(glob: Any, *, location: str = "glob") -> str:
    """Return a valid p5-posix-v1 glob or raise ManifestError."""

    glob = validate_repo_path(glob, location=location)
    if any(character in glob for character in "[]{}!"):
        raise ManifestError(f"{location} uses unsupported classes, braces, or negation")
    if any(prefix in glob for prefix in _EXTGLOB_PREFIXES):
        raise ManifestError(f"{location} uses unsupported extglob syntax")
    if "***" in glob:
        raise ManifestError(f"{location} contains an invalid run of '*' operators")
    return glob


def glob_to_regex(glob: str) -> re.Pattern[str]:
    """Compile a validated p5-posix-v1 glob into a full-path regex."""

    glob = validate_glob(glob)
    pieces = [r"\A"]
    index = 0
    while index < len(glob):
        if glob.startswith("**/", index):
            pieces.append(r"(?:[^/]+/)*")
            index += 3
        elif glob.startswith("**", index):
            pieces.append(r".*")
            index += 2
        elif glob[index] == "*":
            pieces.append(r"[^/]*")
            index += 1
        elif glob[index] == "?":
            pieces.append(r"[^/]")
            index += 1
        else:
            pieces.append(re.escape(glob[index]))
            index += 1
    pieces.append(r"\Z")
    return re.compile("".join(pieces))


def path_matches_glob(path: str, glob: str) -> bool:
    """Return whether a repository path fully matches a p5-posix-v1 glob."""

    path = validate_repo_path(path)
    return glob_to_regex(glob).fullmatch(path) is not None


def _validate_delegation(delegation: Any, rule_glob: str, *, location: str) -> None:
    if not isinstance(delegation, Mapping):
        raise ManifestError(f"{location} must be an object")
    _require_exact_fields(delegation, _DELEGATION_FIELDS, location)

    if delegation["mode"] not in DELEGATION_MODES:
        raise ManifestError(
            f"{location}.mode must be one of {sorted(DELEGATION_MODES)}"
        )
    root = validate_repo_path(delegation["root"], location=f"{location}.root")
    if any(character in root for character in "*?[]{}!"):
        raise ManifestError(f"{location}.root must not contain glob syntax")
    if any(prefix in root for prefix in _EXTGLOB_PREFIXES):
        raise ManifestError(f"{location}.root must not contain extglob syntax")
    if rule_glob != f"{root}/**":
        raise ManifestError(
            f"{location}.root must be covered by its rule glob as '<root>/**'"
        )


def validate_manifest(manifest: Any) -> Mapping[str, Any]:
    """Validate a decoded manifest and return it unchanged."""

    if not isinstance(manifest, Mapping):
        raise ManifestError("manifest must be a JSON object")
    _require_exact_fields(manifest, _TOP_LEVEL_FIELDS, "manifest")

    if (
        not isinstance(manifest["schema_version"], int)
        or isinstance(manifest["schema_version"], bool)
        or manifest["schema_version"] != SCHEMA_VERSION
    ):
        raise ManifestError(f"schema_version must be integer {SCHEMA_VERSION}")
    expected_constants = {
        "match_semantics": MATCH_SEMANTICS,
        "glob_dialect": GLOB_DIALECT,
        "default_classification": DEFAULT_CLASSIFICATION,
        "default_check_class": DEFAULT_CHECK_CLASS,
        "default_disposition": DEFAULT_DISPOSITION,
    }
    for field, expected in expected_constants.items():
        if manifest[field] != expected:
            raise ManifestError(f"{field} must be {expected!r}")

    rules = manifest["rules"]
    if not isinstance(rules, list) or not rules:
        raise ManifestError("rules must be a non-empty ordered array")

    seen_ids: set[str] = set()
    seen_globs: set[str] = set()
    for index, rule in enumerate(rules):
        location = f"rules[{index}]"
        if not isinstance(rule, Mapping):
            raise ManifestError(f"{location} must be an object")

        expected_fields = _RULE_FIELDS
        if rule.get("classification") == "private-workflow-dependent":
            expected_fields = expected_fields | {"delegation"}
        _require_exact_fields(rule, expected_fields, location)

        rule_id = _require_nonempty_string(rule["id"], f"{location}.id")
        glob = validate_glob(rule["glob"], location=f"{location}.glob")
        if rule_id in seen_ids:
            raise ManifestError(f"duplicate rule id {rule_id!r}")
        if glob in seen_globs:
            raise ManifestError(f"duplicate rule glob {glob!r}")
        seen_ids.add(rule_id)
        seen_globs.add(glob)

        if rule["classification"] not in CLASSIFICATIONS:
            raise ManifestError(
                f"{location}.classification must be one of {sorted(CLASSIFICATIONS)}"
            )
        if rule["check_class"] not in CHECK_CLASSES:
            raise ManifestError(
                f"{location}.check_class must be one of {sorted(CHECK_CLASSES)}"
            )
        if rule["disposition"] not in DISPOSITIONS:
            raise ManifestError(
                f"{location}.disposition must be one of {sorted(DISPOSITIONS)}"
            )
        if rule["classification"] == "private-workflow-dependent":
            _validate_delegation(
                rule["delegation"], glob, location=f"{location}.delegation"
            )
    return manifest


def canonical_manifest_bytes(manifest: Any) -> bytes:
    """Return the canonical validated JSON representation used for hashing."""

    validate_manifest(manifest)
    return json.dumps(
        manifest, ensure_ascii=True, separators=(",", ":"), sort_keys=True
    ).encode("utf-8")


def manifest_sha256(manifest: Any) -> str:
    """Return the canonical SHA-256 digest for a validated manifest."""

    return hashlib.sha256(canonical_manifest_bytes(manifest)).hexdigest()


def load_manifest(path: str | Path) -> Mapping[str, Any]:
    """Load and validate a UTF-8 JSON manifest."""

    manifest_path = Path(path)
    try:
        with manifest_path.open("r", encoding="utf-8") as manifest_file:
            manifest = json.load(manifest_file)
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        raise ManifestError(f"cannot load manifest {manifest_path}: {error}") from error
    return validate_manifest(manifest)


def _classify_validated_path(manifest: Mapping[str, Any], path: str) -> dict[str, Any]:
    for rule in manifest["rules"]:
        if path_matches_glob(path, rule["glob"]):
            return {
                "path": path,
                "rule_id": rule["id"],
                "glob": rule["glob"],
                "classification": rule["classification"],
                "check_class": rule["check_class"],
                "disposition": rule["disposition"],
                "delegation": (
                    dict(rule["delegation"]) if "delegation" in rule else None
                ),
            }
    return {
        "path": path,
        "rule_id": None,
        "glob": None,
        "classification": manifest["default_classification"],
        "check_class": manifest["default_check_class"],
        "disposition": manifest["default_disposition"],
        "delegation": None,
    }


def classify_path(manifest: Any, path: str) -> dict[str, Any]:
    """Classify one path using first-match semantics."""

    validated_manifest = validate_manifest(manifest)
    validated_path = validate_repo_path(path)
    return _classify_validated_path(validated_manifest, validated_path)


def classify_paths(manifest: Any, paths: Iterable[str]) -> dict[str, Any]:
    """Classify unique paths and return deterministic path-sorted JSON data."""

    validated_manifest = validate_manifest(manifest)
    validated_paths = [validate_repo_path(path) for path in paths]
    if not validated_paths:
        raise ManifestError("at least one input path is required")
    if len(validated_paths) != len(set(validated_paths)):
        raise ManifestError("input paths must be unique")

    return {
        "schema_version": validated_manifest["schema_version"],
        "manifest_sha256": manifest_sha256(validated_manifest),
        "results": [
            _classify_validated_path(validated_manifest, path)
            for path in sorted(validated_paths)
        ],
    }


def _read_paths_file(path: str | Path) -> list[str]:
    paths_file = Path(path)
    try:
        text = paths_file.read_text(encoding="utf-8")
    except (OSError, UnicodeError) as error:
        raise ManifestError(f"cannot load paths file {paths_file}: {error}") from error
    return text.splitlines()


def _build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Validate and apply the public-transition manifest."
    )
    subparsers = parser.add_subparsers(dest="command", required=True)
    classify = subparsers.add_parser("classify", help="classify repository paths")
    classify.add_argument("--manifest", required=True)
    classify.add_argument("--paths-file", required=True)
    classify.add_argument("--format", choices=("json",), default="json")
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    """Run the deterministic command-line interface."""

    args = _build_parser().parse_args(argv)
    try:
        manifest = load_manifest(args.manifest)
        output = classify_paths(manifest, _read_paths_file(args.paths_file))
    except ManifestError as error:
        print(f"error: {error}", file=sys.stderr)
        return 2

    print(json.dumps(output, ensure_ascii=True, separators=(",", ":"), sort_keys=True))
    if any(
        result["classification"] == DEFAULT_CLASSIFICATION
        for result in output["results"]
    ):
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
