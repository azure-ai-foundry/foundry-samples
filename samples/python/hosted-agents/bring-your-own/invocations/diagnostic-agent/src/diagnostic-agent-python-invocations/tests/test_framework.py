# Copyright (c) Microsoft. All rights reserved.

"""Hermetic contract/framework tests for the diagnostic agent.

These are intentionally network-free (no getaddrinfo, no sockets) so they run
fast and deterministically. They are the conformance safety-net a contributor
runs before adding a probe:

    cd src/diagnostic-agent-python-invocations && python -m unittest discover tests

Schema validation is skipped automatically if ``jsonschema`` is not installed
(it is a dev-only dependency; the probe runtime is stdlib-only).
"""

from __future__ import annotations

import json
import os
import sys
import unittest

# Put the agent source dir (the parent of tests/) on the path.
_SRC = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
if _SRC not in sys.path:
    sys.path.insert(0, _SRC)

from framework import aggregator, registry, report, runner  # noqa: E402
from framework.context import ProbeContext  # noqa: E402
from framework.contract import (  # noqa: E402
    ProbeResult,
    Severity,
    Status,
    error_result,
    finding,
    result,
    status_from_findings,
    worst_status,
)

try:
    import jsonschema  # type: ignore

    _SCHEMA = json.load(open(os.path.join(_SRC, "schema", "report.schema.json")))
except Exception:  # noqa: BLE001
    jsonschema = None  # type: ignore
    _SCHEMA = None


def _ctx(**overrides):
    spec = {"hosts": [], "public_hosts": []}
    spec.update(overrides)
    return ProbeContext.from_spec(spec)


class ContractTests(unittest.TestCase):
    def test_status_from_findings(self):
        self.assertEqual(status_from_findings([]), Status.OK)
        self.assertEqual(status_from_findings([finding("X", Severity.INFO)]), Status.OK)
        self.assertEqual(status_from_findings([finding("X", Severity.WARNING)]), Status.WARN)
        self.assertEqual(status_from_findings([finding("X", Severity.ERROR)]), Status.FAIL)

    def test_worst_status(self):
        rs = [
            result("a.b", 1, Status.OK),
            result("a.c", 1, Status.WARN),
            result("a.d", 1, Status.ERROR),
        ]
        self.assertEqual(worst_status(rs), Status.ERROR)

    def test_error_result_shape(self):
        r = error_result("example.thing", 1, {"host": "h"}, RuntimeError("boom"))
        self.assertEqual(r.status, Status.ERROR)
        self.assertEqual(r.findings[0].code, "PROBE_ERROR")
        d = r.to_dict()
        self.assertEqual(d["status"], "error")
        self.assertIn("findings", d)

    def test_evidence_gating(self):
        r = result("a.b", 1, Status.OK, evidence={"big": "x"})
        self.assertEqual(r.to_dict(include_evidence=True)["evidence"], {"big": "x"})
        self.assertEqual(r.to_dict(include_evidence=False)["evidence"], {})


class RegistryRunnerTests(unittest.TestCase):
    def setUp(self):
        self._saved = list(registry._REGISTRY)
        registry.clear()

    def tearDown(self):
        registry._REGISTRY[:] = self._saved

    def test_namespaced_id_required(self):
        with self.assertRaises(ValueError):
            @registry.register
            class Bad:  # no dot in id
                id = "nodot"
                version = 1

                def applies_to(self, ctx):
                    return True

                def run(self, ctx):
                    return []

    def test_duplicate_id_rejected(self):
        @registry.register
        class A:
            id = "example.thing"
            version = 1

            def applies_to(self, ctx):
                return False

            def run(self, ctx):
                return []

        with self.assertRaises(ValueError):
            @registry.register
            class B:
                id = "example.thing"
                version = 1

                def applies_to(self, ctx):
                    return False

                def run(self, ctx):
                    return []

    def test_isolation_and_ordering(self):
        order_log = []

        @registry.register
        class Good:
            id = "example.good"
            version = 1
            order = 10

            def applies_to(self, ctx):
                return True

            def run(self, ctx):
                order_log.append(self.id)
                return [result(self.id, 1, Status.OK)]

        @registry.register
        class Boom:
            id = "example.boom"
            version = 1
            order = 20

            def applies_to(self, ctx):
                return True

            def run(self, ctx):
                order_log.append(self.id)
                raise RuntimeError("kaboom")

        results = runner.run_all(_ctx())
        by = {r.probe: r for r in results}
        self.assertEqual(by["example.good"].status, Status.OK)  # survived
        self.assertEqual(by["example.boom"].status, Status.ERROR)  # isolated
        self.assertEqual(order_log, ["example.good", "example.boom"])  # order respected

    def test_pre_snapshot_runs_before_all(self):
        events = []

        @registry.register
        class First:
            id = "example.first"
            version = 1
            order = 100  # runs late...

            def applies_to(self, ctx):
                return True

            def pre_snapshot(self, ctx):
                events.append("pre")  # ...but its pre_snapshot is early

            def run(self, ctx):
                events.append("run")
                return [result(self.id, 1, Status.OK)]

        @registry.register
        class Second:
            id = "example.second"
            version = 1
            order = 1

            def applies_to(self, ctx):
                return True

            def run(self, ctx):
                events.append("second_run")
                return [result(self.id, 1, Status.OK)]

        runner.run_all(_ctx())
        # every pre_snapshot precedes every run
        self.assertLess(events.index("pre"), events.index("second_run"))
        self.assertLess(events.index("pre"), events.index("run"))


class RollupTests(unittest.TestCase):
    def _synthetic(self):
        return [
            result(
                "dns.getaddrinfo", 1, Status.FAIL, target={"host": "h"},
                findings=[finding("GAI_FAIL_RAW_OK", Severity.ERROR, "m")],
                evidence={"getaddrinfo": {"status": "FAIL"}},
            ),
            result(
                "dns.raw", 1, Status.WARN, target={"host": "h"},
                findings=[finding("DNS_OK_PRIVATE_INTERMITTENT", Severity.WARNING, "m")],
                evidence={"x": 1},
            ),
            result(
                "container.info", 1, Status.OK, target={"kind": "container"},
                evidence={"hostname": "adc"},
            ),
        ]

    def test_summary_rollup(self):
        s = aggregator.summarize(self._synthetic())
        self.assertEqual(s["status"], "fail")  # worst of ok/warn/fail
        self.assertIn("h", s["targets_failed"])
        self.assertEqual(s["findings_by_severity"].get("error"), 1)
        self.assertEqual(s["findings_by_severity"].get("warning"), 1)
        self.assertTrue(any(f["code"] == "GAI_FAIL_RAW_OK" for f in s["top_findings"]))

    def test_report_schema(self):
        if jsonschema is None:
            self.skipTest("jsonschema not installed (dev-only dependency)")
        ctx = _ctx()
        body = report.build_report(ctx, self._synthetic(), session_id="s", invocation_id="i", elapsed_ms=1.0)
        jsonschema.validate(body, _SCHEMA)
        self.assertEqual(body["schema_version"], 1)
        self.assertIn("summary", body)
        self.assertIn("results", body)


class HostIsolationTests(unittest.TestCase):
    def test_per_host_isolation(self):
        # A crash while probing one host must not abort the others.
        from probes import host as host_mod

        probe = host_mod.HostReachabilityProbe()
        orig = host_mod.probelib.probe_dns

        def boom(h):
            if h == "bad":
                raise RuntimeError("kaboom in host")
            return {"status": "FAIL", "err": "gaierror", "msg": "x"}

        host_mod.probelib.probe_dns = boom
        try:
            ctx = _ctx(hosts=["bad", "good"], raw_dns=False)
            results = probe.run(ctx)  # must NOT raise
        finally:
            host_mod.probelib.probe_dns = orig

        rows = [(r.probe, r.status, r.target.get("host")) for r in results]
        # bad host -> one isolated error result; good host still produced results
        self.assertTrue(any(p == "host.reachability" and s == Status.ERROR and h == "bad" for p, s, h in rows))
        self.assertTrue(any(p == "dns.getaddrinfo" and h == "good" for p, s, h in rows))


if __name__ == "__main__":
    unittest.main()
