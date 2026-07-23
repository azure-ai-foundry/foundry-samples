# Copyright (c) Microsoft. All rights reserved.

"""Low-level, stdlib-only network primitives used by the probes.

These functions are the *proven* implementations that previously lived in
``main.py`` — relocated here unchanged so the probe classes can reuse them and
``main.py`` can stay a thin handler (Single Responsibility). Each returns a plain
dict; the probe classes wrap those dicts into the uniform ``ProbeResult``
envelope and derive findings/metrics from them.

Everything here is stdlib only (``socket``, ``ssl``, ``http.client``,
``urllib``) — the network is the very thing being diagnosed, so the probes must
never depend on an import-time package fetch.
"""

from __future__ import annotations

import http.client
import ipaddress
import socket
import ssl
import time
import urllib.parse
from typing import Any

try:
    from framework import net_probe  # stdlib-only raw DNS / resolver diagnostics
except Exception:  # noqa: BLE001 — never let a probe-module import kill the agent
    net_probe = None  # type: ignore[assignment]

# Environment-variable allowlist for the env dump. Captures only metadata useful
# for triage (region, hosting fabric, project endpoint) and nothing that could
# leak credentials. Anything not on this list is omitted.
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


def is_private_ip(ip_str: str) -> bool:
    try:
        return ipaddress.ip_address(ip_str).is_private
    except ValueError:
        return False


def redact_env_value(name: str, value: str) -> str:
    upper = name.upper()
    if any(s in upper for s in _ENV_REDACT_SUBSTRINGS):
        return f"<redacted len={len(value)}>"
    return value


def read_text(path: str, max_bytes: int = 4096) -> str | None:
    try:
        with open(path, "rb") as f:
            return f.read(max_bytes).decode("utf-8", errors="replace")
    except OSError:
        return None


def default_route() -> str | None:
    """Parse /proc/net/route to find the default gateway."""
    text = read_text("/proc/net/route")
    if not text:
        return None
    for line in text.splitlines()[1:]:
        cols = line.split()
        if len(cols) >= 3 and cols[1] == "00000000":
            try:
                gw_hex = cols[2]
                octets = [int(gw_hex[i : i + 2], 16) for i in (6, 4, 2, 0)]
                return f"{octets[0]}.{octets[1]}.{octets[2]}.{octets[3]} via {cols[0]}"
            except (ValueError, IndexError):
                return None
    return None


def resolvers() -> list[str]:
    text = read_text("/etc/resolv.conf")
    if not text:
        return []
    out: list[str] = []
    for line in text.splitlines():
        parts = line.split()
        if len(parts) >= 2 and parts[0] == "nameserver":
            out.append(parts[1])
    return out


def probe_container_info() -> dict[str, Any]:
    hostname = socket.gethostname()
    try:
        ip = socket.gethostbyname(hostname)
    except OSError as e:
        ip = f"<error: {e}>"
    info: dict[str, Any] = {
        "status": "ok",
        "hostname": hostname,
        "ip": ip,
        "default_route": default_route(),
        "resolvers": resolvers(),
    }
    if net_probe is not None:
        try:
            info["resolv_conf"] = net_probe.parse_resolv_conf()
        except Exception as e:  # noqa: BLE001
            info["resolv_conf"] = {"status": "FAIL", "err": type(e).__name__, "msg": str(e)[:200]}
    return info


def probe_env_dump(environ: dict[str, str]) -> dict[str, Any]:
    out: dict[str, Any] = {"status": "ok", "values": {}}
    values: dict[str, str] = out["values"]
    for k, v in sorted(environ.items()):
        if any(k.startswith(p) for p in _ENV_ALLOWLIST_PREFIXES):
            values[k] = redact_env_value(k, v)
    return out


def probe_dns(host: str) -> dict[str, Any]:
    try:
        infos = socket.getaddrinfo(host, 443, type=socket.SOCK_STREAM)
        ips = sorted({info[4][0] for info in infos})
        any_private = any(is_private_ip(ip) for ip in ips)
        all_private = all(is_private_ip(ip) for ip in ips)
        res: dict[str, Any] = {
            "status": "ok",
            "ips": ips,
            "any_private": any_private,
            "all_private": all_private,
        }
        if not all_private and "privatelink" in host:
            res["hint"] = (
                "Resolved to a non-RFC1918 address; the privatelink zone may not be "
                "linked to this VNet, or the link points at the wrong VNet."
            )
        return res
    except socket.gaierror as e:
        return {
            "status": "FAIL",
            "err": "gaierror",
            "msg": str(e),
            "hint": "DNS lookup failed. Resolver may not have the zone, or DNS traffic is blocked.",
        }
    except Exception as e:  # noqa: BLE001
        return {
            "status": "FAIL",
            "err": type(e).__name__,
            "msg": str(e)[:300],
            "hint": "Unexpected error during getaddrinfo.",
        }


def probe_tcp(ip: str, port: int, timeout_sec: int) -> dict[str, Any]:
    t0 = time.perf_counter()
    try:
        with socket.create_connection((ip, port), timeout=timeout_sec):
            return {"status": "ok", "ip": ip, "port": port, "ms": round((time.perf_counter() - t0) * 1000, 1)}
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
    ctx.minimum_version = ssl.TLSVersion.TLSv1_2  # reject insecure TLS 1.0/1.1
    try:
        with socket.create_connection((ip, port), timeout=timeout_sec) as raw:
            with ctx.wrap_socket(raw, server_hostname=host) as tls:
                cert = tls.getpeercert()
                subject = ", ".join("=".join(p[0]) for p in cert.get("subject", []) if p)
                issuer = ", ".join("=".join(p[0]) for p in cert.get("issuer", []) if p)
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


def probe_http_get(url: str, host_header: str | None, http_timeout_sec: int) -> dict[str, Any]:
    """Plain HTTPS GET. Reports status, headers, body preview. Never sends auth."""
    parsed = urllib.parse.urlparse(url)
    if parsed.scheme != "https":
        return {"status": "FAIL", "err": "scheme", "hint": "Only HTTPS supported."}
    try:
        ctx = ssl.create_default_context()
        ctx.minimum_version = ssl.TLSVersion.TLSv1_2  # reject insecure TLS 1.0/1.1
        conn = http.client.HTTPSConnection(
            parsed.hostname, parsed.port or 443, timeout=http_timeout_sec, context=ctx
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


def probe_direct(target: str, tcp_timeout_sec: int) -> dict[str, Any]:
    """Reachability test for a raw ``ip:port`` (or ``host:port``) WITHOUT DNS."""
    result: dict[str, Any] = {"target": target}
    host = target
    port = 443
    try:
        hostpart, sep, portpart = target.rpartition(":")
        if sep and portpart.isdigit():
            host, port = hostpart, int(portpart)
        host = host.strip("[]")
    except (ValueError, TypeError):
        return {"target": target, "status": "FAIL", "err": "bad_target", "hint": "Use 'ip:port' or 'host:port'."}
    result["tcp"] = probe_tcp(host, port, tcp_timeout_sec)
    return result
