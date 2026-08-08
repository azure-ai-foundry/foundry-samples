from __future__ import annotations

import copy
import importlib.util
import json
import subprocess
import sys
from pathlib import Path

import pytest


REPO_ROOT = Path(__file__).resolve().parents[2]
SCRIPT_PATH = REPO_ROOT / ".github" / "scripts" / "public_transition_manifest.py"
MANIFEST_PATH = REPO_ROOT / ".github" / "public-transition-manifest.json"

SPEC = importlib.util.spec_from_file_location("public_transition_manifest", SCRIPT_PATH)
assert SPEC is not None and SPEC.loader is not None
MATCHER = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MATCHER)


@pytest.fixture
def manifest():
    return MATCHER.load_manifest(MANIFEST_PATH)


def make_manifest(rules=None):
    return {
        "schema_version": 1,
        "match_semantics": "first-match",
        "glob_dialect": "p5-posix-v1",
        "default_classification": "unresolved",
        "default_check_class": "none",
        "default_disposition": "unresolved",
        "rules": rules
        or [
            {
                "id": "samples",
                "glob": "samples/**",
                "classification": "public-ready",
                "check_class": "none",
                "disposition": "public-facing",
            }
        ],
    }


def rule(
    rule_id,
    glob,
    classification="public-ready",
    check_class="none",
    disposition="public-facing",
    delegation=None,
):
    result = {
        "id": rule_id,
        "glob": glob,
        "classification": classification,
        "check_class": check_class,
        "disposition": disposition,
    }
    if classification == "private-workflow-dependent":
        result["delegation"] = delegation or {
            "mode": "tracked-sample-yaml-roots",
            "root": glob.removesuffix("/**"),
        }
    return result


def test_canonical_manifest_schema_and_rule_enums(manifest):
    assert manifest["schema_version"] == 1
    assert manifest["match_semantics"] == "first-match"
    assert manifest["glob_dialect"] == "p5-posix-v1"
    assert manifest["default_classification"] == "unresolved"
    assert manifest["default_check_class"] == "none"
    assert manifest["default_disposition"] == "unresolved"
    assert manifest["rules"]

    for manifest_rule in manifest["rules"]:
        expected_fields = {
            "id",
            "glob",
            "classification",
            "check_class",
            "disposition",
        }
        if manifest_rule["classification"] == "private-workflow-dependent":
            expected_fields.add("delegation")
        assert set(manifest_rule) == expected_fields
        assert manifest_rule["classification"] in MATCHER.CLASSIFICATIONS
        assert manifest_rule["check_class"] in MATCHER.CHECK_CLASSES
        assert manifest_rule["disposition"] in MATCHER.DISPOSITIONS


@pytest.mark.parametrize(
    ("field", "bad_value"),
    [
        ("schema_version", 2),
        ("schema_version", True),
        ("match_semantics", "last-match"),
        ("glob_dialect", "gitignore"),
        ("default_classification", "public-ready"),
        ("default_check_class", "hosted-agents"),
        ("default_disposition", "public-facing"),
        ("rules", []),
    ],
)
def test_rejects_invalid_top_level_values(field, bad_value):
    candidate = make_manifest()
    candidate[field] = bad_value
    with pytest.raises(MATCHER.ManifestError):
        MATCHER.validate_manifest(candidate)


@pytest.mark.parametrize(
    "field",
    [
        "match_semantics",
        "glob_dialect",
        "default_classification",
        "default_check_class",
        "default_disposition",
    ],
)
@pytest.mark.parametrize("bad_value", [[], {}, 7, None])
def test_rejects_non_string_top_level_contract_values(field, bad_value):
    candidate = make_manifest()
    candidate[field] = bad_value
    with pytest.raises(MATCHER.ManifestError):
        MATCHER.validate_manifest(candidate)


@pytest.mark.parametrize(
    "field",
    [
        "schema_version",
        "match_semantics",
        "glob_dialect",
        "default_classification",
        "default_check_class",
        "default_disposition",
        "rules",
    ],
)
def test_rejects_missing_top_level_fields(field):
    candidate = make_manifest()
    del candidate[field]
    with pytest.raises(MATCHER.ManifestError, match="missing"):
        MATCHER.validate_manifest(candidate)


def test_rejects_unexpected_top_level_and_rule_fields():
    candidate = make_manifest()
    candidate["extra"] = True
    with pytest.raises(MATCHER.ManifestError, match="unexpected"):
        MATCHER.validate_manifest(candidate)

    candidate = make_manifest()
    candidate["rules"][0]["extra"] = True
    with pytest.raises(MATCHER.ManifestError, match="unexpected"):
        MATCHER.validate_manifest(candidate)


@pytest.mark.parametrize(
    ("field", "bad_value"),
    [
        ("id", ""),
        ("id", " edge"),
        ("glob", ""),
        ("classification", None),
        ("classification", "unresolved"),
        ("check_class", None),
        ("check_class", "private"),
        ("disposition", None),
        ("disposition", "unresolved"),
    ],
)
def test_rejects_invalid_rule_fields_and_enums(field, bad_value):
    candidate = make_manifest()
    candidate["rules"][0][field] = bad_value
    with pytest.raises(MATCHER.ManifestError):
        MATCHER.validate_manifest(candidate)


@pytest.mark.parametrize(
    "field", ["id", "glob", "classification", "check_class", "disposition"]
)
@pytest.mark.parametrize("bad_value", [[], {}, 7, None])
def test_rejects_non_string_rule_contract_values(field, bad_value):
    candidate = make_manifest()
    candidate["rules"][0][field] = bad_value
    with pytest.raises(MATCHER.ManifestError):
        MATCHER.validate_manifest(candidate)


@pytest.mark.parametrize(
    "field", ["id", "glob", "classification", "check_class", "disposition"]
)
def test_rejects_missing_rule_fields(field):
    candidate = make_manifest()
    del candidate["rules"][0][field]
    with pytest.raises(MATCHER.ManifestError, match="missing"):
        MATCHER.validate_manifest(candidate)


def test_rejects_duplicate_rule_ids_and_globs():
    with pytest.raises(MATCHER.ManifestError, match="duplicate rule id"):
        MATCHER.validate_manifest(
            make_manifest([rule("same", "a/**"), rule("same", "b/**")])
        )
    with pytest.raises(MATCHER.ManifestError, match="duplicate rule glob"):
        MATCHER.validate_manifest(
            make_manifest([rule("one", "a/**"), rule("two", "a/**")])
        )


def test_requires_delegation_only_on_private_dependent_rules():
    private_rule = rule(
        "private",
        "samples/python/hosted-agents/**",
        "private-workflow-dependent",
        "hosted-agents",
    )
    del private_rule["delegation"]
    with pytest.raises(MATCHER.ManifestError, match="delegation"):
        MATCHER.validate_manifest(make_manifest([private_rule]))

    public_rule = rule("public", "samples/**")
    public_rule["delegation"] = {
        "mode": "tracked-sample-yaml-roots",
        "root": "samples",
    }
    with pytest.raises(MATCHER.ManifestError, match="unexpected"):
        MATCHER.validate_manifest(make_manifest([public_rule]))


@pytest.mark.parametrize(
    "delegation",
    [
        None,
        {"mode": "inventory", "root": "samples/python/hosted-agents"},
        {
            "mode": "tracked-sample-yaml-roots",
            "root": "samples/python/hosted-agents/*",
        },
        {
            "mode": "tracked-sample-yaml-roots",
            "root": "./samples/python/hosted-agents",
        },
        {
            "mode": "tracked-sample-yaml-roots",
            "root": "samples/csharp/hosted-agents",
        },
        {
            "mode": "tracked-sample-yaml-roots",
            "root": "samples/python/hosted-agents",
            "targets": [],
        },
    ],
)
def test_rejects_invalid_delegation_objects(delegation):
    candidate_rule = rule(
        "private",
        "samples/python/hosted-agents/**",
        "private-workflow-dependent",
        "hosted-agents",
    )
    candidate_rule["delegation"] = delegation
    with pytest.raises(MATCHER.ManifestError):
        MATCHER.validate_manifest(make_manifest([candidate_rule]))


@pytest.mark.parametrize("field", ["mode", "root"])
@pytest.mark.parametrize("bad_value", [[], {}, 7, None])
def test_rejects_non_string_delegation_values(field, bad_value):
    candidate_rule = rule(
        "private",
        "samples/python/hosted-agents/**",
        "private-workflow-dependent",
        "hosted-agents",
    )
    candidate_rule["delegation"][field] = bad_value
    with pytest.raises(MATCHER.ManifestError):
        MATCHER.validate_manifest(make_manifest([candidate_rule]))


@pytest.mark.parametrize(
    "glob",
    [
        "[ab].txt",
        "a{b,c}.txt",
        "@(a|b).txt",
        "+(a).txt",
        "?(a).txt",
        "*(a).txt",
        "!(a).txt",
        "!private/**",
        r"a\b",
        "***",
        "/root/**",
        "./root/**",
        "../root/**",
        "root/../other",
        "root//file",
        "root/",
        "",
    ],
)
def test_rejects_invalid_glob_syntax_and_normalization(glob):
    with pytest.raises(MATCHER.ManifestError):
        MATCHER.validate_glob(glob)


@pytest.mark.parametrize(
    "path",
    [
        "/root/file",
        "./root/file",
        "../root/file",
        "root/../file",
        "root/./file",
        r"root\file",
        "root//file",
        "root/",
        "",
        " edge",
    ],
)
def test_rejects_invalid_input_path_normalization(path):
    with pytest.raises(MATCHER.ManifestError):
        MATCHER.validate_repo_path(path)


def test_p5_posix_v1_operators_are_full_path_and_case_sensitive():
    assert MATCHER.path_matches_glob("root/a.txt", "root/?.txt")
    assert not MATCHER.path_matches_glob("root/ab.txt", "root/?.txt")
    assert MATCHER.path_matches_glob("root/.txt", "root/*.txt")
    assert MATCHER.path_matches_glob("root/ab.txt", "root/*.txt")
    assert not MATCHER.path_matches_glob("root/nested/ab.txt", "root/*.txt")
    assert MATCHER.path_matches_glob("root/nested/ab.txt", "root/**")
    assert not MATCHER.path_matches_glob("prefix/root/a.txt", "root/**")
    assert MATCHER.path_matches_glob("README.md", "README.md")
    assert not MATCHER.path_matches_glob("readme.md", "README.md")


def test_double_star_slash_matches_zero_or_more_complete_segments():
    assert MATCHER.path_matches_glob(".ci-skip", "**/.ci-skip")
    assert MATCHER.path_matches_glob("samples/.ci-skip", "**/.ci-skip")
    assert MATCHER.path_matches_glob("samples/python/example/.ci-skip", "**/.ci-skip")
    assert not MATCHER.path_matches_glob("samples/example.ci-skip", "**/.ci-skip")


def test_first_match_wins_and_records_provenance():
    candidate = make_manifest(
        [
            rule(
                "override",
                "samples/python/hosted-agents/**",
                "private-workflow-dependent",
                "hosted-agents",
            ),
            rule("fallback", "samples/**"),
        ]
    )
    result = MATCHER.classify_path(
        candidate, "samples/python/hosted-agents/example/sample.yaml"
    )
    assert result == {
        "path": "samples/python/hosted-agents/example/sample.yaml",
        "rule_id": "override",
        "glob": "samples/python/hosted-agents/**",
        "classification": "private-workflow-dependent",
        "check_class": "hosted-agents",
        "disposition": "public-facing",
        "delegation": {
            "mode": "tracked-sample-yaml-roots",
            "root": "samples/python/hosted-agents",
        },
    }


def test_every_canonical_rule_has_expected_first_match(manifest):
    representative_paths = {
        "internal-azure-pipelines": ".azure-pipelines/build.yml",
        "internal-github": ".github/workflows/test.yml",
        "internal-docs": "docs/validation.md",
        "internal-root": "internal/tool.py",
        "internal-public-overlay": "public-overlay/file.txt",
        "internal-readme": "README.md",
        "internal-contributing": "CONTRIBUTING.md",
        "internal-ci-skip": ".ci-skip",
        "internal-code-ci-skip": "samples/demo/.code-ci-skip",
        "python-hosted-agents-private-dependent": (
            "samples/python/hosted-agents/example/sample.yaml"
        ),
        "csharp-hosted-agents-private-dependent": (
            "samples/csharp/hosted-agents/example/sample.yaml"
        ),
        "bicep-private-dependent": (
            "infrastructure/infrastructure-setup-bicep/main.bicep"
        ),
        "samples-public-ready": "samples/python/example/sample.yaml",
        "samples-classic-public-ready": "samples-classic/python/example.py",
        "samples-mistral-public-ready": "samples-mistral/python/example.py",
        "infrastructure-public-ready": "infrastructure/terraform/main.tf",
        "migration-public-ready": "migration/tool.py",
        "infra-public-ready": ".infra/pytest_plugins/plugin.py",
        "gitattributes-public-ready": ".gitattributes",
        "gitignore-public-ready": ".gitignore",
        "pre-commit-config-public-ready": ".pre-commit-config.yaml",
        "code-of-conduct-public-ready": "CODE_OF_CONDUCT.md",
        "license-public-ready": "LICENSE",
        "security-public-ready": "SECURITY.md",
        "support-public-ready": "SUPPORT.md",
        "conftest-public-ready": "conftest.py",
        "dev-requirements-public-ready": "dev-requirements.txt",
        "package-lock-public-ready": "package-lock.json",
        "tox-public-ready": "tox.ini",
    }
    assert set(representative_paths) == {item["id"] for item in manifest["rules"]}
    for expected_rule_id, path in representative_paths.items():
        assert MATCHER.classify_path(manifest, path)["rule_id"] == expected_rule_id


def test_all_current_repository_paths_follow_locked_rules(manifest):
    completed = subprocess.run(
        ["git", "ls-files", "--cached", "--others", "--exclude-standard"],
        cwd=REPO_ROOT,
        check=True,
        capture_output=True,
        text=True,
    )
    tracked_paths = completed.stdout.splitlines()
    output = MATCHER.classify_paths(manifest, tracked_paths)
    unresolved = [
        result["path"]
        for result in output["results"]
        if result["classification"] == "unresolved"
    ]
    assert unresolved == []


def test_delegation_is_path_derived_and_uses_stable_rule_ids(manifest):
    rules_by_id = {item["id"]: item for item in manifest["rules"]}
    python = rules_by_id["python-hosted-agents-private-dependent"]["delegation"]
    csharp = rules_by_id["csharp-hosted-agents-private-dependent"]["delegation"]
    bicep = rules_by_id["bicep-private-dependent"]["delegation"]

    assert python == {
        "mode": "tracked-sample-yaml-roots",
        "root": "samples/python/hosted-agents",
    }
    assert csharp == {
        "mode": "tracked-sample-yaml-roots",
        "root": "samples/csharp/hosted-agents",
    }
    assert bicep == {
        "mode": "rule-root",
        "root": "infrastructure/infrastructure-setup-bicep",
    }
    assert all(set(value) == {"mode", "root"} for value in (python, csharp, bicep))

    completed = subprocess.run(
        ["git", "ls-files", "samples/**/sample.yaml"],
        cwd=REPO_ROOT,
        check=True,
        capture_output=True,
        text=True,
    )
    sample_yaml_paths = completed.stdout.splitlines()

    def discovered_roots(delegation):
        prefix = f"{delegation['root']}/"
        return sorted(
            str(Path(path).parent).replace("\\", "/")
            for path in sample_yaml_paths
            if path.startswith(prefix)
        )

    assert len(discovered_roots(python)) == 4
    assert discovered_roots(csharp) == []
    assert [bicep["root"]] == ["infrastructure/infrastructure-setup-bicep"]


def test_unknown_paths_use_explicit_unresolved_defaults(manifest):
    assert MATCHER.classify_path(manifest, "new-root/file.txt") == {
        "path": "new-root/file.txt",
        "rule_id": None,
        "glob": None,
        "classification": "unresolved",
        "check_class": "none",
        "disposition": "unresolved",
        "delegation": None,
    }


def test_classify_paths_rejects_duplicate_and_empty_inputs(manifest):
    with pytest.raises(MATCHER.ManifestError, match="unique"):
        MATCHER.classify_paths(manifest, ["samples/a", "samples/a"])
    with pytest.raises(MATCHER.ManifestError, match="at least one"):
        MATCHER.classify_paths(manifest, [])


def test_digest_and_result_order_are_deterministic():
    first = make_manifest()
    second = {key: copy.deepcopy(first[key]) for key in reversed(list(first))}
    assert MATCHER.manifest_sha256(first) == MATCHER.manifest_sha256(second)

    output = MATCHER.classify_paths(
        first, ["samples/z/sample.yaml", "samples/a/sample.yaml"]
    )
    assert [result["path"] for result in output["results"]] == [
        "samples/a/sample.yaml",
        "samples/z/sample.yaml",
    ]
    assert len(output["manifest_sha256"]) == 64


def test_importable_api_supports_golden_p4_consumer(manifest):
    def golden_p4_consumer(paths):
        classified = MATCHER.classify_paths(manifest, paths)["results"]
        if any(item["classification"] == "unresolved" for item in classified):
            raise ValueError("P4 fails closed on unresolved sample roots")
        return [
            {
                "path": item["path"],
                "classification": item["classification"],
            }
            for item in classified
            if item["classification"] in {"public-ready", "private-workflow-dependent"}
        ]

    assert golden_p4_consumer(
        [
            "samples/python/quickstart/sample.yaml",
            "samples/python/hosted-agents/example/sample.yaml",
            ".github/private/sample.yaml",
        ]
    ) == [
        {
            "path": "samples/python/hosted-agents/example/sample.yaml",
            "classification": "private-workflow-dependent",
        },
        {
            "path": "samples/python/quickstart/sample.yaml",
            "classification": "public-ready",
        },
    ]


def run_cli(tmp_path, paths, manifest_path=MANIFEST_PATH):
    paths_file = tmp_path / "paths.txt"
    paths_file.write_text("\n".join(paths) + "\n", encoding="utf-8")
    return subprocess.run(
        [
            sys.executable,
            str(SCRIPT_PATH),
            "classify",
            "--manifest",
            str(manifest_path),
            "--paths-file",
            str(paths_file),
            "--format",
            "json",
        ],
        check=False,
        capture_output=True,
        text=True,
    )


def test_cli_emits_deterministic_json_and_returns_zero(tmp_path):
    completed = run_cli(
        tmp_path,
        ["samples/z/sample.yaml", "samples/python/hosted-agents/a/sample.yaml"],
    )
    assert completed.returncode == 0
    assert completed.stderr == ""
    assert completed.stdout == completed.stdout.strip() + "\n"
    output = json.loads(completed.stdout)
    assert output["schema_version"] == 1
    assert len(output["manifest_sha256"]) == 64
    assert [result["path"] for result in output["results"]] == [
        "samples/python/hosted-agents/a/sample.yaml",
        "samples/z/sample.yaml",
    ]


def test_cli_emits_unresolved_json_and_returns_nonzero(tmp_path):
    completed = run_cli(tmp_path, ["new-root/file.txt", "samples/a/sample.yaml"])
    assert completed.returncode == 1
    assert completed.stderr == ""
    output = json.loads(completed.stdout)
    assert [result["path"] for result in output["results"]] == [
        "new-root/file.txt",
        "samples/a/sample.yaml",
    ]
    assert output["results"][0]["classification"] == "unresolved"


def test_cli_rejects_duplicate_inputs_without_success_shaped_output(tmp_path):
    completed = run_cli(tmp_path, ["samples/a", "samples/a"])
    assert completed.returncode == 2
    assert completed.stdout == ""
    assert "unique" in completed.stderr


def test_cli_rejects_malformed_enum_without_traceback(tmp_path):
    malformed = make_manifest()
    malformed["rules"][0]["classification"] = []
    malformed_path = tmp_path / "malformed.json"
    malformed_path.write_text(json.dumps(malformed), encoding="utf-8")

    completed = run_cli(tmp_path, ["samples/a"], malformed_path)
    assert completed.returncode == 2
    assert completed.stdout == ""
    assert completed.stderr.startswith("error: ")
    assert "Traceback" not in completed.stderr
