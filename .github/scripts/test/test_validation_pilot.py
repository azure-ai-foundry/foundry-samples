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
COMPLETENESS = ROOT / "scripts" / "validate-validation-pilot-results.py"
MANIFEST = ROOT / "validation-pilot-matrix.json"
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

    def test_manifest_is_curated_and_supported(self) -> None:
        manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
        self.assertEqual(manifest["schema_version"], 1)
        self.assertEqual(len(manifest["samples"]), 4)
        self.assertEqual(
            {sample["language"] for sample in manifest["samples"]},
            {"csharp", "java", "python", "typescript"},
        )
        for sample in manifest["samples"]:
            self.assertTrue((ROOT.parent / sample["path"]).is_dir(), sample["path"])

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
            self.assertEqual(result["completed_stage"], "L3 validation")
            self.assertTrue(diagnostic.is_file())

    def test_completeness_rejects_missing_matrix_member(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            manifest = root / "manifest.json"
            manifest.write_text(
                json.dumps(
                    {
                        "schema_version": 1,
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
                        "schema_version": 1,
                        "sample": {"id": "one", "path": "samples/python/one", "language": "python", "shape": "fixture"},
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
            self.assertNotEqual(completed.returncode, 0)
            self.assertIn("missing result artifacts: two", completed.stderr)


if __name__ == "__main__":
    unittest.main()
