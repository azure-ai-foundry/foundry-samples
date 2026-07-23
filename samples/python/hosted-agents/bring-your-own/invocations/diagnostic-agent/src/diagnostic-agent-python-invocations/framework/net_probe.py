# Copyright (c) Microsoft. All rights reserved.

"""Stdlib-only raw DNS + resolver diagnostics for the diagnostic agent.

The default ``socket.getaddrinfo`` path collapses the parallel A/AAAA lookups
into a single opaque ``EAI_AGAIN`` ("Temporary failure in name resolution") and
hides the information you actually need to root-cause a private-DNS incident:

* the DNS response code (SERVFAIL vs REFUSED vs NXDOMAIN vs NODATA vs timeout),
* which record type failed (A vs AAAA),
* the CNAME chain (does it terminate at a ``privatelink.*`` name?),
* whether the resolver was even reached,
* whether two resolvers *disagree* (private zone linked to one but not another).

This module hand-rolls a tiny DNS client (``socket`` + ``struct``) so the probe
never depends on an import-time package fetch — the network is the very thing we
are diagnosing. It effectively automates the ``dig A/AAAA/CNAME @<resolver>``
steps an on-call would otherwise ask a customer to run by hand, from inside the
runtime sandbox, and self-classifies each result with a remediation hint.
"""

from __future__ import annotations

import ipaddress
import random
import select
import socket
import struct
import time
from typing import Any

# ── constants ────────────────────────────────────────────────────────────────

RCODES = {
    0: "NOERROR",
    1: "FORMERR",
    2: "SERVFAIL",
    3: "NXDOMAIN",
    4: "NOTIMP",
    5: "REFUSED",
    6: "YXDOMAIN",
    7: "YXRRSET",
    8: "NXRRSET",
    9: "NOTAUTH",
    10: "NOTZONE",
}

QTYPES = {"A": 1, "NS": 2, "CNAME": 5, "SOA": 6, "PTR": 12, "TXT": 16, "AAAA": 28}
QTYPE_NAMES = {v: k for k, v in QTYPES.items()}


# ── resolv.conf ──────────────────────────────────────────────────────────────


def parse_resolv_conf(path: str = "/etc/resolv.conf") -> dict[str, Any]:
    """Return nameservers, search domains and the resolver options that most
    often cause "works from the VM but not the sandbox" surprises (``ndots``,
    ``timeout``, ``attempts``, ``single-request``)."""
    out: dict[str, Any] = {
        "nameservers": [],
        "search": [],
        "options": {},
        "raw": None,
    }
    try:
        with open(path, "rb") as f:
            text = f.read(8192).decode("utf-8", errors="replace")
    except OSError as e:
        out["err"] = f"{type(e).__name__}: {e}"
        return out
    out["raw"] = text
    for line in text.splitlines():
        line = line.strip()
        if not line or line.startswith(("#", ";")):
            continue
        parts = line.split()
        key = parts[0]
        if key == "nameserver" and len(parts) >= 2:
            out["nameservers"].append(parts[1])
        elif key in ("search", "domain"):
            out["search"].extend(parts[1:])
        elif key == "options":
            for opt in parts[1:]:
                if ":" in opt:
                    k, v = opt.split(":", 1)
                    out["options"][k] = v
                else:
                    out["options"][opt] = True
    # ndots defaults to 1 when unset; surface the effective value because a high
    # ndots + many search domains multiplies every failed lookup's latency.
    out["options"].setdefault("ndots", "1")
    return out


# ── wire format ──────────────────────────────────────────────────────────────


def _encode_name(name: str) -> bytes:
    out = b""
    for label in name.rstrip(".").split("."):
        if not label:
            continue
        try:
            b = label.encode("idna")
        except Exception:  # noqa: BLE001 — fall back to raw bytes for odd labels
            b = label.encode("latin1", errors="replace")
        out += bytes([len(b) & 0x3F]) + b
    return out + b"\x00"


def _build_query(qname: str, qtype: int, edns: bool = True) -> tuple[int, bytes]:
    tid = random.randint(0, 0xFFFF)
    flags = 0x0100  # RD (recursion desired)
    arcount = 1 if edns else 0
    header = struct.pack(">HHHHHH", tid, flags, 1, 0, 0, arcount)
    question = _encode_name(qname) + struct.pack(">HH", qtype, 1)  # class IN
    msg = header + question
    if edns:
        # EDNS0 OPT: root name, type 41, UDP payload size 4096, no flags, rdlen 0
        msg += b"\x00" + struct.pack(">HHIH", 41, 4096, 0, 0)
    return tid, msg


def _read_name(data: bytes, offset: int) -> tuple[str, int]:
    labels: list[str] = []
    jumped = False
    next_offset = offset
    guard = 0
    while guard < 128:
        guard += 1
        if offset >= len(data):
            break
        length = data[offset]
        if length & 0xC0 == 0xC0:  # compression pointer
            if offset + 1 >= len(data):
                break
            ptr = struct.unpack(">H", data[offset : offset + 2])[0] & 0x3FFF
            if not jumped:
                next_offset = offset + 2
            offset = ptr
            jumped = True
            continue
        if length == 0:
            offset += 1
            break
        offset += 1
        labels.append(data[offset : offset + length].decode("latin1"))
        offset += length
    return ".".join(labels), (next_offset if jumped else offset)


def _parse_rrs(data: bytes, offset: int, count: int) -> tuple[list[dict[str, Any]], int]:
    rrs: list[dict[str, Any]] = []
    for _ in range(count):
        name, offset = _read_name(data, offset)
        if offset + 10 > len(data):
            break
        rtype, _rclass, ttl, rdlen = struct.unpack(">HHIH", data[offset : offset + 10])
        offset += 10
        rdata = data[offset : offset + rdlen]
        value: Any = None
        try:
            if rtype == 1 and rdlen == 4:
                value = socket.inet_ntoa(rdata)
            elif rtype == 28 and rdlen == 16:
                value = socket.inet_ntop(socket.AF_INET6, rdata)
            elif rtype in (5, 2, 12):  # CNAME / NS / PTR
                value, _ = _read_name(data, offset)
            elif rtype == 6:  # SOA — presence in AUTHORITY marks a NODATA answer
                value, _ = _read_name(data, offset)
        except Exception:  # noqa: BLE001 — never let one RR break parsing
            value = None
        offset += rdlen
        rrs.append(
            {
                "name": name,
                "type": QTYPE_NAMES.get(rtype, str(rtype)),
                "ttl": ttl,
                "data": value,
            }
        )
    return rrs, offset


def _parse_response(data: bytes) -> dict[str, Any]:
    if len(data) < 12:
        return {"err": "short_response"}
    tid, flags, qd, an, ns, _ar = struct.unpack(">HHHHHH", data[:12])
    offset = 12
    for _ in range(qd):  # skip question section
        _, offset = _read_name(data, offset)
        offset += 4
    answers, offset = _parse_rrs(data, offset, an)
    authority, _ = _parse_rrs(data, offset, ns)
    rcode = flags & 0x000F
    return {
        "id": tid,
        "rcode": rcode,
        "rcode_name": RCODES.get(rcode, str(rcode)),
        "tc": bool(flags & 0x0200),
        "aa": bool(flags & 0x0400),
        "ra": bool(flags & 0x0080),
        "answers": answers,
        "authority": authority,
    }


def _query_tcp(resolver: str, msg: bytes, timeout: float, t0: float) -> dict[str, Any]:
    af = socket.AF_INET6 if ":" in resolver else socket.AF_INET
    try:
        with socket.socket(af, socket.SOCK_STREAM) as s:
            s.settimeout(timeout)
            s.connect((resolver, 53))
            s.sendall(struct.pack(">H", len(msg)) + msg)
            hdr = s.recv(2)
            if len(hdr) < 2:
                return {"err": "tcp_short", "transport": "tcp"}
            (rlen,) = struct.unpack(">H", hdr)
            buf = b""
            while len(buf) < rlen:
                chunk = s.recv(rlen - len(buf))
                if not chunk:
                    break
                buf += chunk
        parsed = _parse_response(buf)
        parsed["ms"] = round((time.perf_counter() - t0) * 1000, 1)
        parsed["transport"] = "tcp"
        return parsed
    except socket.timeout:
        return {"err": "timeout", "transport": "tcp", "ms": round((time.perf_counter() - t0) * 1000, 1)}
    except OSError as e:
        return {"err": type(e).__name__, "msg": str(e)[:200], "transport": "tcp"}


def query(resolver: str, qname: str, qtype_name: str, timeout: float = 5.0) -> dict[str, Any]:
    """Send one raw DNS query and return the parsed answer (or an error block)."""
    qtype = QTYPES.get(qtype_name.upper())
    if qtype is None:
        return {"err": "bad_qtype", "qtype": qtype_name}
    _tid, msg = _build_query(qname, qtype, edns=True)
    t0 = time.perf_counter()
    af = socket.AF_INET6 if ":" in resolver else socket.AF_INET
    try:
        with socket.socket(af, socket.SOCK_DGRAM) as s:
            s.settimeout(timeout)
            s.sendto(msg, (resolver, 53))
            data, _ = s.recvfrom(4096)
        parsed = _parse_response(data)
        if parsed.get("tc"):  # truncated → retry over TCP/53
            tcp = _query_tcp(resolver, msg, timeout, t0)
            tcp["truncated_udp"] = True
            return tcp
        parsed["ms"] = round((time.perf_counter() - t0) * 1000, 1)
        parsed["transport"] = "udp"
        return parsed
    except socket.timeout:
        return {"err": "timeout", "transport": "udp", "ms": round((time.perf_counter() - t0) * 1000, 1)}
    except OSError as e:
        return {"err": type(e).__name__, "msg": str(e)[:200], "transport": "udp"}


def query_parallel_dual(resolver: str, qname: str, timeout: float = 5.0) -> dict[str, Any]:
    """Mimic glibc's DEFAULT getaddrinfo behavior: send the A and AAAA queries
    back-to-back on the **same UDP socket / source port**, then wait for both
    replies. This is the one thing a sequential ``dig`` or our per-record probe
    never does - and it is exactly what triggers ``EAI_AGAIN`` when a DNS server
    / stateful middlebox / load balancer mishandles two concurrent queries on one
    5-tuple (no firewall ACL drop involved - a reply is simply lost or mismatched)."""
    tid_a, msg_a = _build_query(qname, QTYPES["A"], edns=True)
    tid_aaaa, msg_aaaa = _build_query(qname, QTYPES["AAAA"], edns=True)
    af = socket.AF_INET6 if ":" in resolver else socket.AF_INET
    t0 = time.perf_counter()
    want = {tid_a: "A", tid_aaaa: "AAAA"}
    try:
        with socket.socket(af, socket.SOCK_DGRAM) as s:
            s.setblocking(False)
            s.sendto(msg_a, (resolver, 53))
            s.sendto(msg_aaaa, (resolver, 53))
            deadline = time.perf_counter() + timeout
            while want and time.perf_counter() < deadline:
                r, _, _ = select.select([s], [], [], max(0.0, deadline - time.perf_counter()))
                if not r:
                    break
                try:
                    data, _ = s.recvfrom(4096)
                except BlockingIOError:
                    continue
                if len(data) >= 2:
                    want.pop(struct.unpack(">H", data[:2])[0], None)
        return {
            "a_ok": tid_a not in want,
            "aaaa_ok": tid_aaaa not in want,
            "both_ok": not want,
            "lost": sorted(want.values()),
            "ms": round((time.perf_counter() - t0) * 1000, 1),
        }
    except OSError as e:
        return {"err": type(e).__name__, "msg": str(e)[:200]}


def parallel_dual_stats(resolver: str, qname: str, timeout: float = 5.0, attempts: int = 10) -> dict[str, Any]:
    """Run the glibc-style parallel A+AAAA probe ``attempts`` times and report the
    both-replies-received rate. A rate below 1.0 - while the sequential per-record
    probe is clean - is the signature of a concurrent-query problem (the cause of
    getaddrinfo EAI_AGAIN that ``dig`` cannot reproduce)."""
    attempts = max(1, int(attempts))
    both = a_lost = aaaa_lost = errs = 0
    lat: list[float] = []
    for _ in range(attempts):
        r = query_parallel_dual(resolver, qname, timeout)
        if r.get("err"):
            errs += 1
            continue
        if r.get("both_ok"):
            both += 1
        if not r.get("a_ok"):
            a_lost += 1
        if not r.get("aaaa_ok"):
            aaaa_lost += 1
        if r.get("ms") is not None:
            lat.append(r["ms"])
    out: dict[str, Any] = {
        "resolver": resolver,
        "attempts": attempts,
        "both_ok": both,
        "a_lost": a_lost,
        "aaaa_lost": aaaa_lost,
        "errors": errs,
        "both_ok_rate": round(both / attempts, 2),
    }
    if lat:
        out["avg_ms"] = round(sum(lat) / len(lat), 1)
        out["max_ms"] = max(lat)
    if both < attempts:
        out["classification"] = "PARALLEL_DUAL_LOSS"
        out["hint"] = (
            f"glibc-style parallel A+AAAA on ONE socket dropped replies ({both}/{attempts} both-ok, "
            f"A_lost={a_lost}, AAAA_lost={aaaa_lost}) while sequential per-record queries succeed. This is "
            "the concurrent-query failure that yields getaddrinfo EAI_AGAIN but NOT dig failures, and it is "
            "not a firewall ACL drop. Fix: make 'options single-request-reopen' take effect on the client "
            "AND/OR fix concurrent-query handling on the DNS server / forwarder / stateful middlebox."
        )
    else:
        out["classification"] = "PARALLEL_DUAL_OK"
        out["hint"] = (
            "Parallel A+AAAA on one socket succeeded every attempt - the getaddrinfo failure is NOT a "
            "same-socket concurrent-query collision; look at glibc attempts/timeout, nsswitch, or an "
            "intermittent loss on this specific name's recursion."
        )
    return out


# ── higher-level helpers ─────────────────────────────────────────────────────


def _answer_ips(resp: dict[str, Any]) -> list[str]:
    return [
        rr["data"]
        for rr in resp.get("answers", [])
        if rr.get("type") in ("A", "AAAA") and rr.get("data")
    ]


def _cname_chain(resp: dict[str, Any]) -> list[str]:
    return [rr["data"] for rr in resp.get("answers", []) if rr.get("type") == "CNAME" and rr.get("data")]


def _has_soa_authority(resp: dict[str, Any]) -> bool:
    return any(rr.get("type") == "SOA" for rr in resp.get("authority", []))


def _is_private(ip: str) -> bool:
    try:
        return ipaddress.ip_address(ip).is_private
    except ValueError:
        return False


def _dig_text(host: str, qtype: str, resolver: str, resp: dict[str, Any]) -> str:
    """A compact ``dig``-style one-liner so the output reads like the command an
    on-call would have run by hand."""
    if resp.get("err"):
        return f";; {qtype} @{resolver}: connection error ({resp['err']})"
    ans = resp.get("answers") or []
    head = f";; ->>HEADER<<- status: {resp.get('rcode_name')}, {len(ans)} answer(s), {resp.get('ms')}ms {resp.get('transport')}"
    lines = [head]
    for rr in ans:
        lines.append(f"{rr.get('name')}\t{rr.get('ttl')}\t{rr.get('type')}\t{rr.get('data')}")
    if not ans and _has_soa_authority(resp):
        lines.append(";; NODATA (name exists, no record of this type) — SOA in AUTHORITY")
    return "\n".join(lines)


def query_over_tcp(resolver: str, qname: str, qtype_name: str, timeout: float = 5.0) -> dict[str, Any]:
    """Force a DNS query over TCP/53 (used to detect UDP-only failures such as
    EDNS/MTU fragmentation drops on a tunneled or hub-forwarded path)."""
    qtype = QTYPES.get(qtype_name.upper())
    if qtype is None:
        return {"err": "bad_qtype", "qtype": qtype_name}
    _tid, msg = _build_query(qname, qtype, edns=True)
    t0 = time.perf_counter()
    return _query_tcp(resolver, msg, timeout, t0)


def _probe_resolver_type(
    resolver: str, host: str, qt: str, timeout: float, attempts: int, tcp_check: bool
) -> dict[str, Any]:
    """Run ``attempts`` UDP queries for one (resolver, record-type), aggregate the
    outcomes (answers, timeout rate, latency spread), and - if any UDP attempt
    timed out - compare against a single TCP/53 query to expose UDP-only drops."""
    attempts = max(1, int(attempts))
    lat: list[float] = []
    timeouts = 0
    errors = 0
    answers: set[str] = set()
    cnames: list[str] = []
    rcodes: list[str] = []
    last: dict[str, Any] = {}
    for _ in range(attempts):
        r = query(resolver, host, qt, timeout=timeout)
        last = r
        err = r.get("err")
        if err == "timeout":
            timeouts += 1
            if r.get("ms") is not None:
                lat.append(r["ms"])
        elif err:
            errors += 1
        else:
            if r.get("ms") is not None:
                lat.append(r["ms"])
            for ip in _answer_ips(r):
                answers.add(ip)
            if not cnames:
                cnames = _cname_chain(r)
            if r.get("rcode_name"):
                rcodes.append(r["rcode_name"])
    successes = attempts - timeouts - errors
    rec: dict[str, Any] = {
        "attempts": attempts,
        "successes": successes,
        "timeouts": timeouts,
        "errors": errors,
        "timeout_rate": round(timeouts / attempts, 2),
        "answers": sorted(answers),
        "cname_chain": cnames,
        "rcodes": sorted(set(rcodes)),
        "rcode": (sorted(set(rcodes))[0] if rcodes else ("timeout" if timeouts else (last.get("err") or "unknown"))),
        "nodata": bool(last.get("rcode_name") == "NOERROR" and not answers and _has_soa_authority(last)),
        "transport": "udp",
    }
    if lat:
        rec["min_ms"] = min(lat)
        rec["max_ms"] = max(lat)
        rec["avg_ms"] = round(sum(lat) / len(lat), 1)
        rec["ms"] = rec["avg_ms"]
    # UDP-only failure detector: on any UDP timeout, probe once over TCP/53.
    if tcp_check and timeouts:
        tcp = query_over_tcp(resolver, host, qt, timeout=timeout)
        tcp_answers = _answer_ips(tcp)
        rec["tcp"] = {
            "rcode": tcp.get("rcode_name") or tcp.get("err"),
            "answers": tcp_answers,
            "ms": tcp.get("ms"),
        }
        if tcp_answers and successes == 0:
            rec["udp_timeout_tcp_ok"] = True
    return rec


def _dig_summary(per_type: dict[str, dict[str, Any]]) -> str:
    """Render an aggregated ``dig``-style summary across attempts."""
    lines: list[str] = []
    for qt, rec in per_type.items():
        if rec.get("answers"):
            lines.append(
                f";; {qt}: status={rec.get('rcode')} {rec.get('answers')} "
                f"({rec.get('successes')}/{rec.get('attempts')} ok, avg {rec.get('avg_ms')}ms)"
            )
        else:
            lines.append(
                f";; {qt}: status={rec.get('rcode')} "
                f"(timeouts {rec.get('timeouts')}/{rec.get('attempts')})"
            )
        for c in rec.get("cname_chain", []):
            lines.append(f"   CNAME -> {c}")
        if rec.get("tcp"):
            lines.append(f"   [tcp/53] rcode={rec['tcp'].get('rcode')} {rec['tcp'].get('answers')}")
    return "\n".join(lines)


def _classify_resolver(host: str, per_type: dict[str, dict[str, Any]]) -> dict[str, str]:
    """Map the aggregated A/AAAA results for one resolver to a fixed vocabulary +
    remediation hint. Intermittency- and transport-aware."""
    a = per_type.get("A", {})
    aaaa = per_type.get("AAAA", {})
    ips = list(a.get("answers") or []) + list(aaaa.get("answers") or [])
    a_attempts = a.get("attempts", 1)
    a_timeouts = a.get("timeouts", 0)

    # UDP drops but TCP works -> fragmentation / MTU on the path.
    if a.get("udp_timeout_tcp_ok") or aaaa.get("udp_timeout_tcp_ok"):
        return {
            "classification": "DNS_UDP_DROP_TCP_OK",
            "hint": "UDP/53 queries time out but the same query succeeds over TCP/53. Large-response "
            "/ EDNS fragmentation is being dropped on the path to the DNS server (common over tunnels, "
            "NVAs, or hub-forwarded paths). Allow UDP fragments / EDNS(0), reduce the EDNS UDP size, or "
            "use a local Private Resolver inbound endpoint to avoid the long UDP path.",
        }

    # Intermittency: the A query sometimes answered and sometimes timed out.
    if a_attempts > 1 and 0 < a_timeouts < a_attempts:
        base = "DNS_OK_PRIVATE_INTERMITTENT" if (ips and all(_is_private(ip) for ip in ips)) else "DNS_INTERMITTENT"
        return {
            "classification": base,
            "hint": f"UNRELIABLE resolution: {a_timeouts}/{a_attempts} attempts timed out while others "
            "answered. The record exists but delivery through this resolver/path is intermittent "
            "(packet loss, forwarder capacity/rate-limit, or a flaky hub/ExpressRoute DNS hop). With a "
            "single resolver in resolv.conf, each drop becomes a hard 'Temporary failure in name "
            "resolution'. Fix the DNS-path reliability and/or add a second resolver or a local Private "
            "Resolver inbound endpoint in this VNet.",
        }

    # No answer at all.
    if not ips:
        if a_attempts and a_timeouts >= a_attempts:
            return {
                "classification": "DNS_TIMEOUT",
                "hint": "No answer before timeout on every attempt. Resolver/forwarder is unreachable "
                "or dropping packets from this subnet - check NSG/UDR/peering to the DNS server and the "
                "forwarder's health/capacity. (Slow failure => path problem, not a missing record.)",
            }
        rcode = a.get("rcode") or aaaa.get("rcode")
        if rcode == "SERVFAIL":
            return {
                "classification": "DNS_SERVFAIL",
                "hint": "Resolver returned SERVFAIL. It is broken for the zone, or its upstream "
                "conditional forwarder for privatelink.* failed. Classic 'works from another subnet, not "
                "the agent subnet' when the forwarder/view is source-scoped.",
            }
        if rcode == "REFUSED":
            return {
                "classification": "DNS_REFUSED",
                "hint": "Resolver REFUSED the query - a source-based ACL/view excludes this subnet. "
                "Authorize the agent-subnet source in the DNS server's view/ACL.",
            }
        if rcode == "NXDOMAIN":
            return {
                "classification": "DNS_NXDOMAIN",
                "hint": "Name does not exist per this resolver. Wrong resolver, or the private zone / "
                "A-record is missing.",
            }
        if a.get("nodata") or aaaa.get("nodata"):
            return {
                "classification": "DNS_NODATA",
                "hint": "Name exists but has no A/AAAA record here (NOERROR + SOA). Record not published "
                "in the zone this resolver serves.",
            }
        return {"classification": "DNS_UNKNOWN", "hint": f"Unclassified (rcode={rcode})."}

    # Got address(es).
    if all(_is_private(ip) for ip in ips):
        return {"classification": "DNS_OK_PRIVATE", "hint": "Resolved to a private IP as expected."}
    if "privatelink" in host or any(
        "privatelink" in c for c in (a.get("cname_chain") or []) + (aaaa.get("cname_chain") or [])
    ):
        return {
            "classification": "DNS_OK_PUBLIC_FOR_PRIVATE",
            "hint": "Name CNAMEs to privatelink.* but resolved to a PUBLIC IP. This resolver does not "
            "serve the private zone for this query. In a centralized/hub-DNS design this is EXPECTED for "
            "a resolver OTHER than the configured hub DNS server - judge by the CONFIGURED resolver.",
        }
    return {"classification": "DNS_OK_PUBLIC", "hint": "Resolved to a public IP."}


def dig_host(
    host: str,
    resolvers: list[str],
    record_types: list[str] | None = None,
    timeout: float = 5.0,
    attempts: int = 1,
    tcp_check: bool = True,
    configured_resolvers: list[str] | None = None,
) -> dict[str, Any]:
    """Automate ``dig <type> @<resolver>`` for every (record_type x resolver),
    optionally repeated ``attempts`` times to expose intermittency, with a
    UDP-vs-TCP/53 comparison to expose fragmentation drops. Keys the verdict on
    the CONFIGURED resolver(s) so a centralized/hub-DNS design (extra comparison
    resolvers) is not misdiagnosed."""
    record_types = record_types or ["A", "AAAA"]
    configured = set(configured_resolvers or resolvers)
    per_resolver: list[dict[str, Any]] = []
    classifications: set[str] = set()

    for rs in resolvers:
        try:
            per_type: dict[str, dict[str, Any]] = {}
            for qt in record_types:
                try:
                    per_type[qt] = _probe_resolver_type(rs, host, qt, timeout, attempts, tcp_check)
                except Exception as e:  # noqa: BLE001 - one record type must not abort the others
                    per_type[qt] = {
                        "attempts": attempts, "answers": [], "cname_chain": [],
                        "rcode": "probe_error", "err": type(e).__name__, "msg": str(e)[:200],
                    }
            cls = _classify_resolver(host, per_type)
            a = per_type.get("A", {})
            aaaa = per_type.get("AAAA", {})
            per_resolver.append(
                {
                    "resolver": rs,
                    "configured": rs in configured,
                    "records": per_type,
                    "cname_chain": (a.get("cname_chain") or aaaa.get("cname_chain") or []),
                    "classification": cls["classification"],
                    "hint": cls["hint"],
                    "dig": _dig_summary(per_type),
                }
            )
            classifications.add(cls["classification"])
        except Exception as e:  # noqa: BLE001 - one resolver must not abort the others
            per_resolver.append(
                {
                    "resolver": rs,
                    "configured": rs in configured,
                    "classification": "PROBE_ERROR",
                    "err": type(e).__name__,
                    "msg": str(e)[:200],
                }
            )
            classifications.add("PROBE_ERROR")

    result: dict[str, Any] = {"host": host, "attempts": attempts, "per_resolver": per_resolver}

    # Primary verdict = the CONFIGURED resolver(s) this workload actually uses.
    configured_pr = [r for r in per_resolver if r["configured"]]
    if configured_pr:
        result["verdict"] = {
            "based_on": [r["resolver"] for r in configured_pr],
            "classifications": sorted({r["classification"] for r in configured_pr}),
        }

    ok_priv = {
        r["resolver"] for r in per_resolver
        if r["classification"] in ("DNS_OK_PRIVATE", "DNS_OK_PRIVATE_INTERMITTENT")
    }
    not_priv = {
        r["resolver"]
        for r in per_resolver
        if r["classification"]
        in ("DNS_OK_PUBLIC", "DNS_OK_PUBLIC_FOR_PRIVATE", "DNS_NXDOMAIN", "DNS_SERVFAIL", "DNS_NODATA")
    }
    if ok_priv and not_priv:
        result["resolver_disagreement"] = {
            "classification": "RESOLVER_DISAGREE",
            "private_from": sorted(ok_priv),
            "not_private_from": sorted(not_priv),
            "hint": "Resolvers return different answers. If 'not_private_from' is only an EXTRA "
            "comparison resolver (e.g. 168.63.129.16 queried from a spoke VNet), this is EXPECTED in a "
            "centralized/hub-DNS design and is NOT the fault. If a CONFIGURED resolver is in that set, "
            "that resolver is not serving the private zone for this workload.",
        }
    result["classifications"] = sorted(classifications)
    return result


def getaddrinfo_vs_raw(host: str, gai_ips: list[str] | None, gai_err: str | None, dig_result: dict[str, Any]) -> dict[str, Any]:
    """Compare the OS resolver (getaddrinfo) with the raw per-resolver truth, to
    catch dual-stack / ndots / nsswitch quirks where the OS call fails even
    though a record resolves cleanly at the wire level."""
    raw_ips: list[str] = []
    for r in dig_result.get("per_resolver", []):
        for rec in r.get("records", {}).values():
            raw_ips.extend(rec.get("answers") or [])
    raw_ips = sorted(set(raw_ips))
    out: dict[str, Any] = {"getaddrinfo_ips": sorted(gai_ips or []), "raw_ips": raw_ips}
    if gai_err and raw_ips:
        out["classification"] = "GAI_FAIL_RAW_OK"
        out["hint"] = (
            f"getaddrinfo failed ({gai_err}) but the record resolves at the wire level ({raw_ips}). "
            "Likely a dual-stack/AAAA, ndots/search-domain, or nsswitch quirk rather than a pure DNS "
            "outage — try 'options single-request' or an explicit A lookup."
        )
    elif not gai_err and gai_ips and raw_ips and set(gai_ips) != set(raw_ips):
        out["classification"] = "GAI_RAW_MISMATCH"
        out["hint"] = "OS resolver and raw queries returned different IPs — caching or multiple resolvers in play."
    else:
        out["classification"] = "GAI_RAW_CONSISTENT"
    return out
