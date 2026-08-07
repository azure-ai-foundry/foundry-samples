#!/usr/bin/env python3
"""Fixture tests for run-validation-health-pilot.py."""

from __future__ import annotations

import base64
import json
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


SCRIPT = Path(__file__).resolve().parents[1] / "run-validation-health-pilot.py"
RENDERER = Path(__file__).resolve().parents[1] / "render-validation-health.py"
WORKFLOW = Path(__file__).resolve().parents[2] / "workflows" / "validation-health-pilot.yml"
SHA = "0123456789abcdef0123456789abcdef01234567"
C_SHARP = "samples/csharp/quickstart/chat-with-agent"
PYTHON = "samples/python/quickstart/chat-with-agent"


def state_marker(payload: dict) -> str:
    encoded = base64.urlsafe_b64encode(
        json.dumps(payload, separators=(",", ":"), sort_keys=True).encode()
    ).decode()
    return f"<!-- validation-health-state-v1:{encoded} -->"


class PilotRunnerTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        for sample in (C_SHARP, PYTHON):
            sample_dir = self.root / sample
            sample_dir.mkdir(parents=True)
            (sample_dir / "sample.yaml").write_text("name: fixture\n", encoding="utf-8")
        self.config = self.root / "config.json"
        self.config.write_text(
            json.dumps(
                {
                    "schema_version": 1,
                    "repository": "microsoft-foundry/foundry-samples",
                    "samples": [C_SHARP, PYTHON],
                }
            ),
            encoding="utf-8",
        )
        self.validator = self.root / "validator.sh"
        self.yq = self.root / "yq.sh"

    def tearDown(self) -> None:
        self.temp.cleanup()

    def write_tools(self, *, l3_csharp: int, l3_python: int, csharp_l4: bool, l4: int) -> None:
        self.validator.write_text(
            f"""#!/usr/bin/env bash
set -u
args="$*"
if [[ "$args" == *"--level 4"* ]]; then
  exit {l4}
fi
if [[ "$args" == *"{C_SHARP}"* ]]; then
  exit {l3_csharp}
fi
if [[ "$args" == *"{PYTHON}"* ]]; then
  exit {l3_python}
fi
exit 2
""",
            encoding="utf-8",
            newline="\n",
        )
        self.yq = self.root / "fake_yq.py"
        self.yq.write_text(
            (
                "import sys\n"
                f"print('true' if {C_SHARP!r} in sys.argv[-1] else 'false')\n"
                if csharp_l4
                else "print('false')\n"
            ),
            encoding="utf-8",
            newline="\n",
        )

    def run_pilot(self, previous: dict | str | None = None) -> subprocess.CompletedProcess[str]:
        output = self.root / "results.json"
        command = [
            sys.executable,
            str(SCRIPT),
            "--config",
            str(self.config),
            "--repo-root",
            str(self.root),
            "--validator",
            str(self.validator),
            "--yq",
            sys.executable,
            str(self.yq),
            "--output",
            str(output),
            "--source-sha",
            SHA,
            "--run-at",
            "2026-08-07T21:00:00Z",
            "--evidence-url",
            "https://github.com/example/actions/runs/1",
        ]
        if previous is not None:
            body = self.root / "previous.md"
            if isinstance(previous, str):
                body.write_text(previous, encoding="utf-8")
            else:
                body.write_text(state_marker(previous), encoding="utf-8")
            command.extend(["--previous-body", str(body)])
        environment = os.environ.copy()
        environment["SKIP_PROVISION"] = "false"
        completed = subprocess.run(
            command, capture_output=True, text=True, env=environment
        )
        completed.output_path = output  # type: ignore[attr-defined]
        return completed

    def test_results_include_completed_levels_and_clear_undeclared_l4(self) -> None:
        self.write_tools(l3_csharp=0, l3_python=1, csharp_l4=True, l4=2)
        previous = {
            "schema_version": 1,
            "source_sha": SHA,
            "results": {
                PYTHON: {
                    "l4": {
                        "status": "pass",
                        "run_at": "2026-08-01T00:00:00Z",
                    }
                }
            },
        }
        completed = self.run_pilot(previous)
        self.assertEqual(completed.returncode, 1, completed.stderr)
        payload = json.loads(completed.output_path.read_text(encoding="utf-8"))
        self.assertEqual(payload["results"][C_SHARP]["l3"]["status"], "pass")
        self.assertEqual(payload["results"][C_SHARP]["l4"]["status"], "error")
        self.assertEqual(payload["results"][PYTHON]["l3"]["status"], "failure")
        self.assertNotIn("l4", payload["results"][PYTHON])

    def test_previous_l4_is_preserved_when_declared_but_l3_blocks_run(self) -> None:
        self.write_tools(l3_csharp=1, l3_python=0, csharp_l4=True, l4=0)
        previous_l4 = {
            "status": "pass",
            "run_at": "2026-08-01T00:00:00Z",
        }
        previous = {
            "schema_version": 1,
            "source_sha": SHA,
            "results": {C_SHARP: {"l4": previous_l4}},
        }
        completed = self.run_pilot(previous)
        self.assertEqual(completed.returncode, 1, completed.stderr)
        payload = json.loads(completed.output_path.read_text(encoding="utf-8"))
        self.assertEqual(payload["results"][C_SHARP]["l4"], previous_l4)

    def test_all_pass_with_no_l4_declarations_exits_zero(self) -> None:
        self.write_tools(l3_csharp=0, l3_python=0, csharp_l4=False, l4=0)
        completed = self.run_pilot()
        self.assertEqual(completed.returncode, 0, completed.stderr)
        payload = json.loads(completed.output_path.read_text(encoding="utf-8"))
        self.assertNotIn("l4", payload["results"][C_SHARP])
        self.assertNotIn("l4", payload["results"][PYTHON])
        dashboard = self.root / "dashboard.md"
        rendered = subprocess.run(
            [
                sys.executable,
                str(RENDERER),
                "--config",
                str(self.config),
                "--repo-root",
                str(self.root),
                "--results",
                str(completed.output_path),
                "--output",
                str(dashboard),
                "--generated-at",
                "2026-08-07T21:01:00Z",
            ],
            capture_output=True,
            text=True,
        )
        self.assertEqual(rendered.returncode, 0, rendered.stderr)
        markdown = dashboard.read_text(encoding="utf-8")
        self.assertEqual(markdown.count("✅ Pass"), 2)
        self.assertEqual(markdown.count("⚪ Never run"), 2)
        self.assertIn("validation-health-state-v1:", markdown)

    def test_malformed_hidden_state_fails_without_overwriting(self) -> None:
        self.write_tools(l3_csharp=0, l3_python=0, csharp_l4=False, l4=0)
        completed = self.run_pilot(
            "<!-- validation-health-state-v1:not-valid-base64 -->"
        )
        self.assertEqual(completed.returncode, 2)
        self.assertIn("hidden state marker is invalid", completed.stderr)
        self.assertFalse(completed.output_path.exists())

    def test_corrupted_hidden_state_prefix_fails_without_overwriting(self) -> None:
        self.write_tools(l3_csharp=0, l3_python=0, csharp_l4=False, l4=0)
        completed = self.run_pilot(
            "<!-- validation-health-state-v1:abc.def -->"
        )
        self.assertEqual(completed.returncode, 2)
        self.assertIn("hidden state marker is invalid", completed.stderr)
        self.assertFalse(completed.output_path.exists())

    def test_workflow_is_manual_main_only_and_preserves_verdict(self) -> None:
        workflow = WORKFLOW.read_text(encoding="utf-8")
        self.assertIn("workflow_dispatch:", workflow)
        self.assertNotIn("schedule:", workflow)
        self.assertNotIn("pull_request:", workflow)
        self.assertIn("ref: main", workflow)
        self.assertIn("if: github.ref == 'refs/heads/main'", workflow)
        self.assertIn("SKIP_PROVISION: 'false'", workflow)
        self.assertIn("issues: write", workflow)
        self.assertNotIn("id-token: write", workflow)
        self.assertNotIn("azure/login", workflow)
        self.assertNotIn("environment: L4-validation", workflow)
        self.assertIn("Refuse undeclared credential boundary", workflow)
        self.assertIn("gh issue edit", workflow)
        self.assertIn("if: always()", workflow)
        self.assertIn('exit "$VALIDATION_RC"', workflow)


if __name__ == "__main__":
    unittest.main()
