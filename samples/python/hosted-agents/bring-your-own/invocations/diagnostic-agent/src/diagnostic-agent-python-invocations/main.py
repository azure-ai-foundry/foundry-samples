# Copyright (c) Microsoft. All rights reserved.

"""Diagnostic Agent — network and environment diagnostics from inside a hosted-agent
runtime sandbox.

On each invocation, runs a configurable set of probes against caller-supplied
hostnames and returns a single structured JSON response. Designed to answer:

    "What can the runtime inside the delegated agent subnet actually reach?"

Typical use case (the question that motivated this image):

    "From inside agent-subnet-*, does nslookup <customer>.azurecr.io resolve
     to a private IP? Does curl -v https://<customer>.azurecr.io/v2/ return
     a 401? If not, where does it break?"

The agent is deliberately stdlib-only for the probe code itself
(``socket``, ``ssl``, ``urllib``, ``http.client``) — the network is the very
thing being diagnosed, so the probes must not depend on an import-time package
fetch.

Required environment variables: none (intentional). The agent does not call
LLMs and does not require a Foundry project endpoint.

POST body contract (all fields optional)::

    {
      "hosts": ["<acr>.azurecr.io", "<acr>.westus2.data.azurecr.io"],
      "public_hosts": ["https://www.microsoft.com/",
                       "https://management.azure.com/",
                       "https://login.microsoftonline.com/"],
            "include_env_dump":       true,
            "include_container_info": true,
      "tcp_timeout_sec":  5,
      "http_timeout_sec": 10
    }

If the body is empty, a default profile runs: container info + env dump
+ a small fixed list of public Azure endpoints. No private hosts
are probed unless explicitly requested.

The response is **always HTTP 200**. Every probe — and every top-level
section of the handler — is wrapped in its own try/except and reports
failure via a ``status`` / ``err`` / ``msg`` / ``hint`` block inside the
response JSON. Even a crash in the handler itself returns 200 with an
``error`` block, so the caller (which often cannot read non-2xx bodies)
always gets actionable diagnostic data.
"""

from __future__ import annotations

import http.client
import ipaddress
import json
import logging
import os
import socket
import ssl
import sys
import time
import traceback
import urllib.parse
from datetime import datetime, timezone
from typing import Any

from starlette.requests import Request
from starlette.responses import JSONResponse

from azure.ai.agentserver.invocations import InvocationAgentServerHost

# Emit all logs to stdout so they show up in the hosted-agent log stream.
# basicConfig is a no-op if the root logger already has handlers (e.g. when
# the host configures uvicorn logging first), so also force the level.
logging.basicConfig(
    level=os.environ.get("DEBUG_AGENT_LOG_LEVEL", "INFO"),
    format="%(asctime)s %(levelname)s %(name)s %(message)s",
    stream=sys.stdout,
)
logging.getLogger().setLevel(os.environ.get("DEBUG_AGENT_LOG_LEVEL", "INFO"))
logger = logging.getLogger("diagnostic_agent")

# Environment-variable allowlist for the env dump. Captures only metadata
# that's useful for triaging (region, hosting fabric, project endpoint) and
# nothing that could leak credentials. Anything not on this list is omitted.
_ENV_ALLOWLIST_PREFIXES = (
    "FOUNDRY_",
    "AZURE_",
    "KUBERNETES_",
    "POD_",
    "NODE_",
    "HOSTNAME",
    "REGION",
    "LOCATION",
)
_ENV_REDACT_SUBSTRINGS = (
    "KEY",
    "SECRET",
    "PASSWORD",
    "TOKEN",
    "CONNECTION_STRING",
    "SAS",
)

_DEFAULT_PUBLIC_HOSTS = [
    "https://www.microsoft.com/",
    "https://management.azure.com/metadata/endpoints?api-version=2020-09-01",
    "https://login.microsoftonline.com/common/v2.0/.well-known/openid-configuration",
]


# ── helpers ──────────────────────────────────────────────────────────────────


def _is_private_ip(ip_str: str) -> bool:
    try:
        return ipaddress.ip_address(ip_str).is_private
    except ValueError:
        return False


def _redact_env_value(name: str, value: str) -> str:
    upper = name.upper()
    if any(s in upper for s in _ENV_REDACT_SUBSTRINGS):
        return f"<redacted len={len(value)}>"
    return value


def _read_text(path: str, max_bytes: int = 4096) -> str | None:
    try:
        with open(path, "rb") as f:
            return f.read(max_bytes).decode("utf-8", errors="replace")
    except OSError:
        return None


def _default_route() -> str | None:
    """Parse /proc/net/route to find the default gateway."""
    text = _read_text("/proc/net/route")
    if not text:
        return None
    for line in text.splitlines()[1:]:
        cols = line.split()
        # Iface Destination Gateway Flags ...
        if len(cols) >= 3 and cols[1] == "00000000":
            try:
                gw_hex = cols[2]
                octets = [int(gw_hex[i : i + 2], 16) for i in (6, 4, 2, 0)]
                return f"{octets[0]}.{octets[1]}.{octets[2]}.{octets[3]} via {cols[0]}"
            except (ValueError, IndexError):
                return None
    return None


def _resolvers() -> list[str]:
    text = _read_text("/etc/resolv.conf")
    if not text:
        return []
    out: list[str] = []
    for line in text.splitlines():
        parts = line.split()
        if len(parts) >= 2 and parts[0] == "nameserver":
            out.append(parts[1])
    return out


# ── individual probes ────────────────────────────────────────────────────────


def probe_container_info() -> dict[str, Any]:
    try:
        hostname = socket.gethostname()
        try:
            ip = socket.gethostbyname(hostname)
        except OSError as e:
            ip = f"<error: {e}>"
        return {
            "status": "ok",
            "hostname": hostname,
            "ip": ip,
            "default_route": _default_route(),
            "resolvers": _resolvers(),
        }
    except Exception as e:  # noqa: BLE001 — diagnostic; never let a probe kill the response
        return {
            "status": "FAIL",
            "err": type(e).__name__,
            "msg": str(e)[:300],
            "hint": "Reading container hostname / /proc state failed unexpectedly.",
        }


def probe_env_dump() -> dict[str, Any]:
    try:
        out: dict[str, Any] = {"status": "ok", "values": {}}
        values: dict[str, str] = out["values"]
        for k, v in sorted(os.environ.items()):
            if any(k.startswith(p) for p in _ENV_ALLOWLIST_PREFIXES):
                values[k] = _redact_env_value(k, v)
        return out
    except Exception as e:  # noqa: BLE001
        return {
            "status": "FAIL",
            "err": type(e).__name__,
            "msg": str(e)[:300],
            "hint": "Iterating os.environ failed unexpectedly.",
        }


def probe_dns(host: str) -> dict[str, Any]:
    try:
        infos = socket.getaddrinfo(host, 443, type=socket.SOCK_STREAM)
        ips = sorted({info[4][0] for info in infos})
        any_private = any(_is_private_ip(ip) for ip in ips)
        all_private = all(_is_private_ip(ip) for ip in ips)
        result: dict[str, Any] = {
            "status": "ok",
            "ips": ips,
            "any_private": any_private,
            "all_private": all_private,
        }
        # For ``privatelink.*``-style names, public IPs are a smell
        if not all_private and "privatelink" in host:
            result["hint"] = (
                "Resolved to a non-RFC1918 address; the privatelink zone may not be "
                "linked to this VNet, or the link points at the wrong VNet."
            )
        return result
    except socket.gaierror as e:
        return {
            "status": "FAIL",
            "err": "gaierror",
            "msg": str(e),
            "hint": "DNS lookup failed. Resolver may not have the zone, or DNS traffic is blocked.",
        }


def probe_tcp(ip: str, port: int, timeout_sec: int) -> dict[str, Any]:
    t0 = time.perf_counter()
    try:
        with socket.create_connection((ip, port), timeout=timeout_sec):
            return {
                "status": "ok",
                "ip": ip,
                "port": port,
                "ms": round((time.perf_counter() - t0) * 1000, 1),
            }
    except socket.timeout:
        return {
            "status": "FAIL",
            "ip": ip,
            "port": port,
            "ms": round((time.perf_counter() - t0) * 1000, 1),
            "err": "timeout",
            "hint": "TCP SYN silently dropped. Likely a network security rule, routing issue, or firewall drop.",
        }
    except ConnectionRefusedError:
        return {
            "status": "FAIL",
            "ip": ip,
            "port": port,
            "err": "refused",
            "hint": "Connection refused. PE may be in Disconnected state, or an upstream device is sending RST.",
        }
    except OSError as e:
        return {
            "status": "FAIL",
            "ip": ip,
            "port": port,
            "err": type(e).__name__,
            "msg": str(e)[:200],
            "hint": "OS-level network error (no route, host unreachable). Check UDR / VNet peering.",
        }


def probe_tls(host: str, ip: str, port: int, timeout_sec: int) -> dict[str, Any]:
    t0 = time.perf_counter()
    ctx = ssl.create_default_context()
    try:
        with socket.create_connection((ip, port), timeout=timeout_sec) as raw:
            with ctx.wrap_socket(raw, server_hostname=host) as tls:
                cert = tls.getpeercert()
                subject = ", ".join(
                    "=".join(p[0]) for p in cert.get("subject", []) if p
                )
                issuer = ", ".join(
                    "=".join(p[0]) for p in cert.get("issuer", []) if p
                )
                sans = [v for k, v in cert.get("subjectAltName", []) if k == "DNS"]
                return {
                    "status": "ok",
                    "ms": round((time.perf_counter() - t0) * 1000, 1),
                    "version": tls.version(),
                    "cipher": tls.cipher()[0] if tls.cipher() else None,
                    "cert_subject": subject,
                    "cert_issuer": issuer,
                    "cert_sans": sans[:10],
                }
    except ssl.SSLCertVerificationError as e:
        return {
            "status": "FAIL",
            "err": "SSLCertVerificationError",
            "msg": str(e)[:300],
            "hint": "Cert verify failed. A firewall is likely doing TLS interception — bypass *.azurecr.io / *.azure.com.",
        }
    except ssl.SSLError as e:
        return {
            "status": "FAIL",
            "err": "SSLError",
            "msg": str(e)[:300],
            "hint": "TLS handshake failed mid-stream. A network middlebox may be breaking SNI; ensure TLS passthrough.",
        }
    except (socket.timeout, OSError) as e:
        return {
            "status": "FAIL",
            "err": type(e).__name__,
            "msg": str(e)[:200],
            "hint": "TCP succeeded but TLS phase failed. Could be a network device reset or a transient issue.",
        }


def probe_http_get(
    url: str, host_header: str | None, http_timeout_sec: int
) -> dict[str, Any]:
    """Plain HTTPS GET. Reports status, headers, body preview. Never sends auth."""
    parsed = urllib.parse.urlparse(url)
    if parsed.scheme != "https":
        return {"status": "FAIL", "err": "scheme", "hint": "Only HTTPS supported."}
    try:
        ctx = ssl.create_default_context()
        conn = http.client.HTTPSConnection(
            parsed.hostname,
            parsed.port or 443,
            timeout=http_timeout_sec,
            context=ctx,
        )
        path = parsed.path or "/"
        if parsed.query:
            path = f"{path}?{parsed.query}"
        headers = {"User-Agent": "foundry-diagnostic-agent/1.0", "Accept": "*/*"}
        if host_header:
            headers["Host"] = host_header
        t0 = time.perf_counter()
        conn.request("GET", path, headers=headers)
        resp = conn.getresponse()
        body = resp.read(2048)
        elapsed = round((time.perf_counter() - t0) * 1000, 1)
        # Surface a small subset of headers that are useful for triage.
        useful_headers = {
            k.lower(): v
            for k, v in resp.getheaders()
            if k.lower()
            in (
                "www-authenticate",
                "server",
                "content-type",
                "docker-distribution-api-version",
                "x-ms-request-id",
                "x-ms-correlation-request-id",
            )
        }
        return {
            "status": "ok",
            "url": url,
            "code": resp.status,
            "reason": resp.reason,
            "ms": elapsed,
            "headers": useful_headers,
            "body_preview": body.decode("utf-8", errors="replace")[:400],
        }
    except Exception as e:  # noqa: BLE001
        return {
            "status": "FAIL",
            "url": url,
            "err": type(e).__name__,
            "msg": str(e)[:300],
            "hint": "HTTPS request failed. See per-layer hints in TCP/TLS probes for the same host.",
        }


def probe_host(host: str, tcp_timeout_sec: int, http_timeout_sec: int) -> dict[str, Any]:
    """Composite probe: DNS -> TCP/443 -> TLS/443 -> HTTPS GET /v2/ (or /)."""
    result: dict[str, Any] = {"host": host}
    dns = probe_dns(host)
    result["dns"] = dns
    if dns["status"] != "ok" or not dns.get("ips"):
        return result
    ip = dns["ips"][0]
    result["tcp_443"] = probe_tcp(ip, 443, tcp_timeout_sec)
    if result["tcp_443"]["status"] != "ok":
        return result
    result["tls_443"] = probe_tls(host, ip, 443, tcp_timeout_sec)
    if result["tls_443"]["status"] != "ok":
        return result
    # For ACR-shaped hosts, /v2/ is the canonical reachability test and returns
    # 401 with a useful WWW-Authenticate header. For everything else hit /.
    path = "/v2/" if host.endswith(".azurecr.io") or ".data.azurecr.io" in host else "/"
    result["http_get"] = probe_http_get(
        f"https://{host}{path}", host_header=None, http_timeout_sec=http_timeout_sec
    )
    return result


# ── handler ──────────────────────────────────────────────────────────────────

app = InvocationAgentServerHost()


def _parse_body(body: bytes) -> dict[str, Any]:
    if not body:
        return {}
    try:
        data = json.loads(body)
        return data if isinstance(data, dict) else {}
    except json.JSONDecodeError:
        # Plain-text body (e.g. from the Foundry portal chat UI) — treat the
        # text as a single hostname so users can paste an FQDN and get answers.
        text = body.decode("utf-8", errors="replace").strip()
        if text:
            return {"hosts": [text]}
        return {}


@app.invoke_handler
async def handle_invoke(request: Request) -> JSONResponse:
    session_id = getattr(request.state, "session_id", None)
    invocation_id = getattr(request.state, "invocation_id", None)
    t_start = time.perf_counter()
    checks: dict[str, Any] = {}
    section_errors: list[dict[str, Any]] = []

    def _run_section(name: str, fn) -> None:
        """Run a probe section under its own try/except so one failure cannot
        suppress sibling diagnostics."""
        try:
            checks[name] = fn()
        except Exception as e:  # noqa: BLE001 — diagnostic; surface everything
            tb = traceback.format_exc()
            logger.error(
                "section FAIL name=%s err=%s msg=%s\n%s",
                name,
                type(e).__name__,
                str(e)[:500],
                tb,
            )
            checks[name] = {
                "status": "FAIL",
                "err": type(e).__name__,
                "msg": str(e)[:500],
                "traceback": tb,
            }
            section_errors.append({"section": name, "err": type(e).__name__})

    try:
        body = await request.body()
        spec = _parse_body(body)

        tcp_timeout_sec = int(spec.get("tcp_timeout_sec") or 5)
        http_timeout_sec = int(spec.get("http_timeout_sec") or 10)

        hosts = spec.get("hosts") or []
        public_hosts = spec.get("public_hosts")
        if public_hosts is None:
            public_hosts = _DEFAULT_PUBLIC_HOSTS

        include_env = spec.get("include_env_dump", True)
        include_container = spec.get("include_container_info", True)

        logger.info(
            "invoke start invocation=%s session=%s body_len=%d hosts=%d public=%d",
            invocation_id,
            session_id,
            len(body),
            len(hosts),
            len(public_hosts),
        )
        logger.debug("invoke spec=%s", spec)

        if include_container:
            _run_section("container", probe_container_info)
            logger.info(
                "probe container status=%s ip=%s",
                checks["container"].get("status"),
                checks["container"].get("ip"),
            )
        if include_env:
            _run_section("env", probe_env_dump)
            logger.info(
                "probe env status=%s keys=%d",
                checks["env"].get("status"),
                len(checks["env"].get("values") or {}),
            )
        if hosts:
            def _hosts_section() -> list[dict[str, Any]]:
                results: list[dict[str, Any]] = []
                for h in hosts:
                    try:
                        results.append(
                            probe_host(h, tcp_timeout_sec, http_timeout_sec)
                        )
                    except Exception as e:  # noqa: BLE001
                        results.append(
                            {
                                "host": h,
                                "status": "FAIL",
                                "err": type(e).__name__,
                                "msg": str(e)[:300],
                            }
                        )
                return results

            _run_section("hosts", _hosts_section)
            for r in checks["hosts"] if isinstance(checks.get("hosts"), list) else []:
                logger.info(
                    "probe host %s dns=%s tcp=%s tls=%s http=%s",
                    r.get("host"),
                    (r.get("dns") or {}).get("status"),
                    (r.get("tcp_443") or {}).get("status"),
                    (r.get("tls_443") or {}).get("status"),
                    (r.get("http_get") or {}).get("code")
                    or (r.get("http_get") or {}).get("status"),
                )
        if public_hosts:
            def _public_section() -> list[dict[str, Any]]:
                results: list[dict[str, Any]] = []
                for u in public_hosts:
                    try:
                        results.append(probe_http_get(u, None, http_timeout_sec))
                    except Exception as e:  # noqa: BLE001
                        results.append(
                            {
                                "url": u,
                                "status": "FAIL",
                                "err": type(e).__name__,
                                "msg": str(e)[:300],
                            }
                        )
                return results

            _run_section("public_hosts", _public_section)
            for r in (
                checks["public_hosts"]
                if isinstance(checks.get("public_hosts"), list)
                else []
            ):
                logger.info(
                    "probe public %s code=%s status=%s",
                    r.get("url"),
                    r.get("code"),
                    r.get("status"),
                )

        elapsed_ms = round((time.perf_counter() - t_start) * 1000, 1)
        logger.info(
            "invoke ok invocation=%s session=%s ms=%s checks=%s section_errors=%d",
            invocation_id,
            session_id,
            elapsed_ms,
            list(checks.keys()),
            len(section_errors),
        )

        return JSONResponse(
            {
                "status": "ok" if not section_errors else "partial",
                "agent_session_id": session_id,
                "invocation_id": invocation_id,
                "timestamp_utc": datetime.now(timezone.utc).isoformat(),
                "elapsed_ms": elapsed_ms,
                "section_errors": section_errors,
                "checks": checks,
            }
        )
    except Exception as e:  # noqa: BLE001 — last-chance; still return 200 with details
        elapsed_ms = round((time.perf_counter() - t_start) * 1000, 1)
        tb = traceback.format_exc()
        logger.error(
            "invoke FAIL invocation=%s session=%s ms=%s err=%s msg=%s\n%s",
            invocation_id,
            session_id,
            elapsed_ms,
            type(e).__name__,
            str(e)[:500],
            tb,
        )
        return JSONResponse(
            {
                "status": "handler_error",
                "agent_session_id": session_id,
                "invocation_id": invocation_id,
                "timestamp_utc": datetime.now(timezone.utc).isoformat(),
                "elapsed_ms": elapsed_ms,
                "checks": checks,
                "error": {
                    "type": type(e).__name__,
                    "message": str(e)[:500],
                    "traceback": tb,
                },
            }
        )


if __name__ == "__main__":
    app.run()
