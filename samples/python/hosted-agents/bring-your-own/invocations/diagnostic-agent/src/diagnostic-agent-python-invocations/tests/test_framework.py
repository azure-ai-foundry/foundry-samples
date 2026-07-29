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
from unittest import mock

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
from framework.streaming import stream_report, wants_stream  # noqa: E402

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

        @registry.register
        class Later:
            id = "example.later"
            version = 1
            order = 30

            def applies_to(self, ctx):
                return True

            def run(self, ctx):
                order_log.append(self.id)
                return [result(self.id, 1, Status.OK)]

        results = runner.run_all(_ctx())
        by = {r.probe: r for r in results}
        self.assertEqual(by["example.good"].status, Status.OK)  # survived
        self.assertEqual(by["example.boom"].status, Status.ERROR)  # isolated
        self.assertEqual(by["example.later"].status, Status.OK)  # later probe still ran
        self.assertEqual(order_log, ["example.good", "example.boom", "example.later"])

        body = report.build_report(_ctx(), results, session_id="s", invocation_id="i", elapsed_ms=1.0)
        self.assertEqual(body["status"], "partial")
        self.assertEqual(body["summary"]["probes_errored"], ["example.boom"])
        self.assertIn("example.later", body["summary"]["probes_run"])

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

    def test_malformed_probe_output_is_isolated(self):
        @registry.register
        class Malformed:
            id = "example.malformed"
            version = 1
            order = 10

            def applies_to(self, ctx):
                return True

            def run(self, ctx):
                return [None]

        @registry.register
        class Later:
            id = "example.after-malformed"
            version = 1
            order = 20

            def applies_to(self, ctx):
                return True

            def run(self, ctx):
                return [result(self.id, 1, Status.OK)]

        ctx = _ctx()
        results = runner.run_all(ctx)
        body = report.build_report(ctx, results, session_id="s", invocation_id="i", elapsed_ms=1.0)

        self.assertEqual([row["probe"] for row in body["results"]], ["example.malformed", "example.after-malformed"])
        self.assertEqual(body["results"][0]["status"], "error")
        self.assertEqual(body["results"][0]["findings"][0]["code"], "PROBE_ERROR")
        self.assertEqual(body["results"][1]["status"], "ok")
        self.assertEqual(body["status"], "partial")


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


class DnsPropagationTests(unittest.TestCase):
    class FakeClock:
        def __init__(self):
            self.now = 0.0

        def monotonic(self):
            return self.now

        def sleep(self, seconds):
            self.now += seconds

    def test_zero_duration_is_preserved(self):
        ctx = _ctx(
            dns_propagation_duration_sec=0,
            dns_propagation_interval_sec=0,
            dns_propagation_threshold_sec=0,
        )

        self.assertEqual(ctx.dns_propagation_duration_sec, 0)
        self.assertEqual(ctx.dns_propagation_interval_sec, 0.1)
        self.assertEqual(ctx.dns_propagation_threshold_sec, 0)

    def test_disabled_by_default(self):
        from probes.dns_propagation import DnsPropagationProbe

        self.assertFalse(DnsPropagationProbe().applies_to(_ctx(hosts=["example.test"])))

    def test_pre_snapshot_captures_initial_dns_before_other_probes(self):
        from probes import dns_propagation

        events = []

        @registry.register
        class LaterProbe:
            id = "example.later"
            version = 1
            order = 2

            def applies_to(self, ctx):
                return True

            def run(self, ctx):
                events.append("later")
                return []

        ctx = _ctx(
            hosts=["example.test"],
            dns_propagation_probe=True,
            dns_propagation_duration_sec=0,
        )
        with mock.patch.object(
            dns_propagation.probelib,
            "probe_dns",
            side_effect=lambda _host: events.append("dns") or {"status": "ok", "ips": ["10.0.0.1"]},
        ):
            results = runner.run_all(ctx)

        self.assertEqual(events[:2], ["dns", "later"])
        self.assertEqual(results[0].probe, "dns.propagation")
        self.assertEqual(results[0].evidence["attempts"][0]["elapsed_sec"], 0.0)

    def test_multiple_hosts_share_the_same_observation_window(self):
        from probes import dns_propagation

        clock = self.FakeClock()
        ctx = _ctx(
            hosts=["one.test", "two.test"],
            dns_propagation_probe=True,
            dns_propagation_duration_sec=10,
            dns_propagation_interval_sec=5,
            dns_propagation_threshold_sec=10,
        )
        with (
            mock.patch.object(dns_propagation.time, "monotonic", clock.monotonic),
            mock.patch.object(dns_propagation.time, "sleep", clock.sleep),
            mock.patch.object(
                dns_propagation.probelib,
                "probe_dns",
                return_value={"status": "ok", "ips": ["10.0.0.1"]},
            ),
        ):
            probe = dns_propagation.DnsPropagationProbe()
            probe.pre_snapshot(ctx)
            rows = probe.run(ctx)

        self.assertEqual(clock.now, 10.0)
        self.assertEqual(len(rows), 2)
        self.assertEqual(
            [[attempt["elapsed_sec"] for attempt in row.evidence["attempts"]] for row in rows],
            [[0.0, 5.0, 10.0], [0.0, 5.0, 10.0]],
        )

    def test_samples_at_deadline_when_interval_does_not_divide_duration(self):
        from probes import dns_propagation

        clock = self.FakeClock()
        ctx = _ctx(
            hosts=["example.test"],
            dns_propagation_probe=True,
            dns_propagation_duration_sec=10,
            dns_propagation_interval_sec=6,
            dns_propagation_threshold_sec=10,
        )
        with (
            mock.patch.object(dns_propagation.time, "monotonic", clock.monotonic),
            mock.patch.object(dns_propagation.time, "sleep", clock.sleep),
            mock.patch.object(
                dns_propagation.probelib,
                "probe_dns",
                return_value={"status": "ok", "ips": ["10.0.0.1"]},
            ),
        ):
            probe = dns_propagation.DnsPropagationProbe()
            probe.pre_snapshot(ctx)
            row = probe.run(ctx)[0]

        self.assertEqual([attempt["elapsed_sec"] for attempt in row.evidence["attempts"]], [0.0, 6.0, 10.0])

    def test_recovery_after_threshold_warns(self):
        from probes import dns_propagation

        clock = self.FakeClock()

        def resolve(_host):
            if clock.now < 20:
                return {"status": "FAIL", "err": "gaierror", "msg": "temporary failure"}
            return {"status": "ok", "ips": ["10.0.0.1"]}

        ctx = _ctx(
            hosts=["example.test"],
            dns_propagation_probe=True,
            dns_propagation_duration_sec=20,
            dns_propagation_interval_sec=5,
            dns_propagation_threshold_sec=15,
        )
        with (
            mock.patch.object(dns_propagation.time, "monotonic", clock.monotonic),
            mock.patch.object(dns_propagation.time, "sleep", clock.sleep),
            mock.patch.object(dns_propagation.probelib, "probe_dns", resolve),
        ):
            row = dns_propagation.DnsPropagationProbe().run(ctx)[0]

        self.assertEqual(row.status, Status.WARN)
        self.assertEqual([f.code for f in row.findings], ["DNS_PROPAGATION_DELAY"])
        self.assertEqual(row.metrics["first_success_after_sec"], 20.0)
        self.assertEqual(row.metrics["persisted_past_threshold"], 1)
        self.assertEqual([a["elapsed_sec"] for a in row.evidence["attempts"]], [0.0, 5.0, 10.0, 15.0, 20.0])

    def test_recovery_before_threshold_still_reports_initial_instability(self):
        from probes import dns_propagation

        clock = self.FakeClock()

        def resolve(_host):
            if clock.now < 5:
                return {"status": "FAIL", "err": "gaierror", "msg": "temporary failure"}
            return {"status": "ok", "ips": ["10.0.0.1"]}

        ctx = _ctx(
            hosts=["example.test"],
            dns_propagation_probe=True,
            dns_propagation_duration_sec=20,
            dns_propagation_interval_sec=5,
            dns_propagation_threshold_sec=15,
        )
        with (
            mock.patch.object(dns_propagation.time, "monotonic", clock.monotonic),
            mock.patch.object(dns_propagation.time, "sleep", clock.sleep),
            mock.patch.object(dns_propagation.probelib, "probe_dns", resolve),
        ):
            row = dns_propagation.DnsPropagationProbe().run(ctx)[0]

        self.assertEqual(row.status, Status.WARN)
        self.assertEqual([f.code for f in row.findings], ["DNS_INITIAL_INSTABILITY"])
        self.assertEqual(row.metrics["first_success_after_sec"], 5.0)
        self.assertEqual(row.metrics["persisted_past_threshold"], 0)

    def test_failure_for_full_window_fails(self):
        from probes import dns_propagation

        clock = self.FakeClock()
        ctx = _ctx(
            hosts=["example.test"],
            dns_propagation_probe=True,
            dns_propagation_duration_sec=20,
            dns_propagation_interval_sec=5,
            dns_propagation_threshold_sec=15,
        )
        failure = {"status": "FAIL", "err": "gaierror", "msg": "temporary failure"}
        with (
            mock.patch.object(dns_propagation.time, "monotonic", clock.monotonic),
            mock.patch.object(dns_propagation.time, "sleep", clock.sleep),
            mock.patch.object(dns_propagation.probelib, "probe_dns", return_value=failure),
        ):
            row = dns_propagation.DnsPropagationProbe().run(ctx)[0]

        self.assertEqual(row.status, Status.FAIL)
        self.assertEqual([f.code for f in row.findings], ["DNS_FAILURE_PERSISTED"])
        self.assertNotIn("first_success_after_sec", row.metrics)
        self.assertEqual(row.metrics["persisted_past_threshold"], 1)


class StreamingTests(unittest.IsolatedAsyncioTestCase):
    def test_stream_selection_is_opt_in(self):
        self.assertFalse(wants_stream({}))
        self.assertTrue(wants_stream({"stream": True}))
        self.assertTrue(wants_stream({}, "application/json, text/event-stream"))

    async def test_heartbeat_precedes_final_report(self):
        import time

        def build_report():
            time.sleep(0.03)
            return {"schema_version": 1, "status": "ok"}

        events = []
        async for frame in stream_report(build_report, "inv-1", heartbeat_sec=0.01):
            events.append(json.loads(frame.removeprefix("data: ").strip()))

        self.assertEqual(events[0], {"type": "started", "invocation_id": "inv-1"})
        self.assertTrue(any(event["type"] == "heartbeat" for event in events))
        self.assertEqual(events[-2]["type"], "report")
        self.assertEqual(events[-2]["report"], {"schema_version": 1, "status": "ok"})
        self.assertEqual(events[-1], {"type": "done", "invocation_id": "inv-1"})

    async def test_worker_failure_becomes_error_event(self):
        def build_report():
            raise RuntimeError("boom")

        events = []
        async for frame in stream_report(build_report, "inv-2", heartbeat_sec=0.01):
            events.append(json.loads(frame.removeprefix("data: ").strip()))

        self.assertEqual([event["type"] for event in events], ["started", "error", "done"])
        self.assertEqual(events[1]["error"], {"type": "RuntimeError", "message": "boom"})


if __name__ == "__main__":
    unittest.main()
