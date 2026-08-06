FROM python:3.12-slim

WORKDIR /app
COPY . user_agent/
WORKDIR /app/user_agent

RUN if [ -f requirements.txt ]; then \
        pip install -r requirements.txt; \
    else \
        echo "No requirements.txt found"; \
    fi

EXPOSE 8088

# vNext (ADC): Append the egress proxy CA cert to system and Python cert bundles
# so that outbound HTTPS (App Insights, Azure services, etc.) works correctly.

# Precompile Python bytecode at build time so cold starts don't pay for it.
# These base images ship almost none of the standard library precompiled
# (~2% of stdlib .py files, on both Debian-slim and Azure Linux), so without
# this every new container recompiles hundreds of stdlib modules on first
# import. Dependencies are best-effort (`|| true`) so a stray unparsable file
# vendored by a third-party package can't fail the build; application code is
# compiled strictly so a syntax error surfaces at build time rather than at
# container start.
# PYTHONDONTWRITEBYTECODE is cleared for these commands only: it blocks
# bytecode *writes*, not *reads*, so images that set it still benefit.
RUN PYTHONDONTWRITEBYTECODE= python -m compileall -q $(python -c "import sysconfig as s; print(s.get_paths()['stdlib'], s.get_paths()['purelib'])") || true; \
    PYTHONDONTWRITEBYTECODE= python -m compileall -q .

CMD bash -c '\
  if [ -f /etc/ssl/certs/adc-egress-proxy-ca.crt ]; then \
    cat /etc/ssl/certs/adc-egress-proxy-ca.crt >> /etc/ssl/certs/ca-certificates.crt && \
    cat /etc/ssl/certs/adc-egress-proxy-ca.crt >> $(python -c "import certifi; print(certifi.where())"); \
  fi && \
  python main.py'
