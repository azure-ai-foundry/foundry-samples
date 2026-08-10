#!/usr/bin/env python3
from __future__ import annotations

import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


SCRIPT = Path(__file__).resolve().parents[1] / "render-validation-report.py"
SAMPLE_A = "samples/python/quickstart/a"
SAMPLE_B = "samples/csharp/quickstart/b"


class ReportTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.results = self.root / "results"
        self.results.mkdir()
        self.expected = self.root / "expected.json"
        self.expected.write_text(
            json.dumps(
                {
                    "schema_version": 1,
                    "samples": [
                        {"id": "a", "path": SAMPLE_A, "language": "python", "shape": "quickstart"},
                        {"id": "b", "path": SAMPLE_B, "language": "csharp", "shape": "quickstart"},
                    ]
                }
            ),
            encoding="utf-8",
        )
        self.output = self.root / "summary.md"

    def tearDown(self) -> None:
        self.temp.cleanup()

    def run_report(self) -> subprocess.CompletedProcess[str]:
        return subprocess.run(
            [
                sys.executable,
                str(SCRIPT),
                "--results-dir",
                str(self.results),
                "--expected-samples",
                str(self.expected),
                "--output",
                str(self.output),
                "--run-url",
                "https://github.com/example/repo/actions/runs/42",
            ],
            capture_output=True,
            text=True,
        )

    def write_result(self, sample: str, outcome: str = "passed") -> None:
        sample_id = "a" if sample == SAMPLE_A else "b"
        sample_dir = self.results / sample_id
        sample_dir.mkdir()
        (sample_dir / "diagnostics.log").write_text("diagnostic\n", encoding="utf-8")
        (sample_dir / "sample-result.json").write_text(
            json.dumps(
                {
                    "schema_version": 1,
                    "sample": {
                        "id": sample_id,
                        "path": sample,
                        "language": "python" if sample_id == "a" else "csharp",
                        "shape": "quickstart",
                    },
                    "outcome": outcome,
                    "completed_stage": "L3 validation",
                    "duration_seconds": 12.5,
                    "diagnostic_reference": "diagnostics.log",
                    "artifact_reference": f"validation-pilot-{sample_id}",
                    "completed_at": "2026-08-10T19:22:33Z",
                    "run": {
                        "repository": "example/repo",
                        "workflow": "validation pilot",
                        "run_id": "42",
                        "run_attempt": "1",
                        "sha": "abc",
                        "ref": "refs/heads/main",
                        "started_at": "2026-08-10T19:22:00Z",
                    },
                }
            ),
            encoding="utf-8",
        )

    def test_renders_all_outcomes_and_run_freshness(self) -> None:
        self.write_result(SAMPLE_A, "passed")
        self.write_result(SAMPLE_B, "sample failure")
        completed = self.run_report()
        self.assertEqual(completed.returncode, 0, completed.stderr)
        body = self.output.read_text(encoding="utf-8")
        self.assertIn("✅ Passed", body)
        self.assertIn("❌ Sample failure", body)
        self.assertIn("2026-08-10 19:22:33 UTC", body)
        self.assertIn("Run evidence:", body)
        self.assertEqual(body.count("`samples/"), 2)

    def test_missing_expected_artifact_publishes_partial_summary_and_fails(self) -> None:
        self.write_result(SAMPLE_A)
        completed = self.run_report()
        self.assertEqual(completed.returncode, 1)
        body = self.output.read_text(encoding="utf-8")
        self.assertIn("expected result artifact is missing", body)
        self.assertIn("⚠️ Infrastructure/error", body)

    def test_malformed_artifact_publishes_error_row_and_fails(self) -> None:
        bad = self.results / "bad"
        bad.mkdir()
        (bad / "sample-result.json").write_text("{", encoding="utf-8")
        completed = self.run_report()
        self.assertEqual(completed.returncode, 1)
        body = self.output.read_text(encoding="utf-8")
        self.assertIn("invalid artifact: sample-result.json", body)
        self.assertIn("⚠️ Infrastructure/error", body)


if __name__ == "__main__":
    unittest.main()
