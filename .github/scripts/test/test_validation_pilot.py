#!/usr/bin/env python3
"""Focused contract tests for the representative validation pilot producer."""

from __future__ import annotations

import json
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
RUNNER = ROOT / "scripts" / "run-validation-pilot.py"
DISCOVERY = ROOT / "scripts" / "discover-validation-samples.py"
COMPLETENESS = ROOT / "scripts" / "validate-validation-pilot-results.py"
WORKFLOW = ROOT / "workflows" / "validation-pilot.yml"


class ValidationPilotTests(unittest.TestCase):
    def test_workflow_calls_report_after_completeness(self) -> None:
        workflow = WORKFLOW.read_text(encoding="utf-8")
        self.assertIn("  report:", workflow)
        self.assertIn("needs: completeness", workflow)
        self.assertIn("if: ${{ always() && !cancelled() }}", workflow)
        self.assertIn("uses: ./.github/workflows/validation-report.yml", workflow)
        self.assertIn(
            "results-artifact: validation-pilot-run-${{ github.run_id }}-${{ github.run_attempt }}",
            workflow,
        )

    def test_discovery_covers_full_inventory_and_explicitly_skips_rust(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            manifest = root / "manifest.json"
            matrix = root / "matrix.json"
            build_readiness_matrix = root / "build-readiness-matrix.json"
            live_service_matrix = root / "live-service-matrix.json"
            completed = subprocess.run(
                [
                    sys.executable,
                    str(DISCOVERY),
                    "--root",
                    str(ROOT.parent),
                    "--manifest",
                    str(manifest),
                    "--matrix",
                    str(matrix),
                    "--build-readiness-matrix",
                    str(build_readiness_matrix),
                    "--live-service-matrix",
                    str(live_service_matrix),
                ],
                capture_output=True,
                text=True,
            )
            self.assertEqual(completed.returncode, 0, completed.stderr)
            payload = json.loads(manifest.read_text(encoding="utf-8"))
            self.assertEqual(payload["schema_version"], 2)
            sample_metadata = list((ROOT.parent / "samples").glob("**/sample.yaml"))
            expected_unsupported = sum(
                path.relative_to(ROOT.parent / "samples").parts[0]
                not in {"csharp", "java", "python", "typescript", "javascript"}
                for path in sample_metadata
            )
            self.assertEqual(len(payload["samples"]), len(sample_metadata))
            self.assertEqual(
                sum(not value["eligible"] for value in payload["validation"].values()),
                expected_unsupported,
            )
            self.assertEqual(
                {value["validator_language"] for value in payload["validation"].values() if value["validator_language"]},
                {"csharp", "java", "python", "typescript"},
            )
            self.assertTrue(all(sample["shape"] == "full-fleet" for sample in payload["samples"]))
            declared_live_service_paths = {
                sample["path"]
                for sample in payload["samples"]
                if payload["validation"][sample["id"]][
                    "live_service_validation_declared"
                ]
            }
            self.assertEqual(
                declared_live_service_paths,
                {
                    "samples/csharp/quickstart/responses",
                    "samples/python/quickstart/responses",
                },
            )
            self.assertEqual(json.loads(matrix.read_text(encoding="utf-8"))["include"], [
                {**sample, **payload["validation"][sample["id"]]} for sample in payload["samples"]
            ])
            self.assertTrue(
                all(
                    not sample["live_service_validation_declared"]
                    for sample in json.loads(
                        build_readiness_matrix.read_text()
                    )["include"]
                )
            )
            self.assertEqual(
                {
                    sample["path"]
                    for sample in json.loads(live_service_matrix.read_text())[
                        "include"
                    ]
                },
                declared_live_service_paths,
            )

    def test_workflow_isolates_declared_live_service_warm_project_jobs(self) -> None:
        workflow = WORKFLOW.read_text(encoding="utf-8")
        self.assertEqual(workflow.count("environment: L4-validation"), 1)
        self.assertIn(
            "matrix: ${{ fromJSON(needs.discover.outputs.build_readiness_matrix) }}",
            workflow,
        )
        self.assertIn(
            "matrix: ${{ fromJSON(needs.discover.outputs.live_service_matrix) }}",
            workflow,
        )
        self.assertIn(
            "AZURE_AI_PROJECT_ENDPOINT: ${{ vars.AZURE_AI_PROJECT_ENDPOINT }}",
            workflow,
        )
        self.assertIn(
            "MODEL_DEPLOYMENT: ${{ vars.MODEL_DEPLOYMENT }}",
            workflow,
        )
        self.assertIn('SKIP_PROVISION: "true"', workflow)
        self.assertIn('python -m pip install -r "${{ matrix.path }}/requirements.txt"', workflow)

    def test_discovery_rejects_legacy_l4_metadata_with_migration_message(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            sample = root / "samples" / "python" / "legacy"
            sample.mkdir(parents=True)
            (sample / "sample.yaml").write_text(
                "name: legacy\nl4:\n  command: \"true\"\n",
                encoding="utf-8",
            )
            completed = subprocess.run(
                [
                    sys.executable,
                    str(DISCOVERY),
                    "--root",
                    str(root),
                    "--manifest",
                    str(root / "manifest.json"),
                    "--matrix",
                    str(root / "matrix.json"),
                ],
                capture_output=True,
                text=True,
            )
            self.assertNotEqual(completed.returncode, 0)
            self.assertIn(
                "rename it to 'live_service_validation'", completed.stderr
            )

    def test_discovery_recognizes_indented_live_service_metadata(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            sample = root / "samples" / "python" / "indented"
            sample.mkdir(parents=True)
            (sample / "sample.yaml").write_text(
                "  name: indented\n"
                "  live_service_validation:\n"
                "    command: \"true\"\n",
                encoding="utf-8",
            )
            completed = subprocess.run(
                [
                    sys.executable,
                    str(DISCOVERY),
                    "--root",
                    str(root),
                    "--manifest",
                    str(root / "manifest.json"),
                    "--matrix",
                    str(root / "matrix.json"),
                ],
                capture_output=True,
                text=True,
            )
            self.assertEqual(completed.returncode, 0, completed.stderr)
            manifest = json.loads(
                (root / "manifest.json").read_text(encoding="utf-8")
            )
            self.assertTrue(
                manifest["validation"]["python-indented"][
                    "live_service_validation_declared"
                ]
            )

    def test_sample_failure_is_a_complete_valid_result(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            validator = root / "validator.sh"
            validator.write_text("import sys\nsys.exit(1)\n", encoding="utf-8")
            output = root / "sample-result.json"
            diagnostic = root / "diagnostics.log"
            completed = subprocess.run(
                [
                    sys.executable,
                    str(RUNNER),
                    "--sample-id",
                    "fixture",
                    "--language",
                    "python",
                    "--shape",
                    "fixture",
                    "--sample-path",
                    "samples/python/fixture",
                    "--validator",
                    str(validator),
                    "--bash",
                    sys.executable,
                    "--output",
                    str(output),
                    "--diagnostic",
                    str(diagnostic),
                ],
                capture_output=True,
                text=True,
            )
            self.assertEqual(completed.returncode, 0, completed.stderr)
            result = json.loads(output.read_text(encoding="utf-8"))
            self.assertEqual(result["outcome"], "sample failure")
            self.assertEqual(result["completed_stage"], "build readiness validation")
            self.assertTrue(diagnostic.is_file())

    def test_completeness_rejects_missing_matrix_member(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            manifest = root / "manifest.json"
            manifest.write_text(
                json.dumps(
                    {
                        "schema_version": 2,
                        "samples": [
                            {"id": "one", "path": "samples/python/one", "language": "python", "shape": "fixture"},
                            {"id": "two", "path": "samples/java/two", "language": "java", "shape": "fixture"},
                        ],
                    }
                ),
                encoding="utf-8",
            )
            artifacts = root / "artifacts" / "validation-pilot-one"
            artifacts.mkdir(parents=True)
            (artifacts / "diagnostics.log").write_text("diagnostic\n", encoding="utf-8")
            (artifacts / "sample-result.json").write_text(
                json.dumps(
                    {
                        "schema_version": 2,
                        "sample": {"id": "one", "path": "samples/python/one", "language": "python", "shape": "fixture"},
                        "outcome": "passed",
                        "completed_stage": "build readiness validation",
                        "duration_seconds": 1,
                        "diagnostic_reference": "diagnostics.log",
                        "artifact_reference": "sample-result.json",
                        "completed_at": "2026-08-10T00:00:00Z",
                        "run": {"run_id": "1"},
                    }
                ),
                encoding="utf-8",
            )
            completed = subprocess.run(
                [
                    sys.executable,
                    str(COMPLETENESS),
                    "--manifest",
                    str(manifest),
                    "--artifacts",
                    str(root / "artifacts"),
                ],
                capture_output=True,
                text=True,
            )
            self.assertNotEqual(completed.returncode, 0)
            self.assertIn("missing result artifacts: two", completed.stderr)

    def test_completeness_accepts_historical_schema_one_artifacts(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            sample = {
                "id": "one",
                "path": "samples/python/one",
                "language": "python",
                "shape": "fixture",
            }
            manifest = root / "manifest.json"
            manifest.write_text(
                json.dumps({"schema_version": 1, "samples": [sample]}),
                encoding="utf-8",
            )
            artifact = root / "artifacts" / "validation-pilot-one"
            artifact.mkdir(parents=True)
            (artifact / "diagnostics.log").write_text(
                "diagnostic\n", encoding="utf-8"
            )
            (artifact / "sample-result.json").write_text(
                json.dumps(
                    {
                        "schema_version": 1,
                        "sample": sample,
                        "outcome": "passed",
                        "completed_stage": "L3 validation",
                        "duration_seconds": 1,
                        "diagnostic_reference": "diagnostics.log",
                        "artifact_reference": "sample-result.json",
                        "completed_at": "2026-08-10T00:00:00Z",
                        "run": {"run_id": "1"},
                    }
                ),
                encoding="utf-8",
            )
            completed = subprocess.run(
                [
                    sys.executable,
                    str(COMPLETENESS),
                    "--manifest",
                    str(manifest),
                    "--artifacts",
                    str(root / "artifacts"),
                ],
                capture_output=True,
                text=True,
            )
            self.assertEqual(completed.returncode, 0, completed.stderr)
            summary = json.loads(
                (root / "artifacts" / "run-summary.json").read_text(
                    encoding="utf-8"
                )
            )
            self.assertEqual(summary["schema_version"], 1)


if __name__ == "__main__":
    unittest.main()
