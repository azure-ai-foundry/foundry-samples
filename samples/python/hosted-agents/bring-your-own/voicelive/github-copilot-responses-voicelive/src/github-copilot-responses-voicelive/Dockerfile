FROM python:3.13-slim

WORKDIR /app

COPY . user_agent/
WORKDIR /app/user_agent

RUN pip install --no-input --upgrade pip && \
    pip install --no-input -r requirements.txt

# Resolve the SDK's pinned CLI runtime at build time instead of on the first
# invocation, when the container filesystem may be read-only.
RUN python -m copilot download-runtime && \
    cli_path="$(find /root/.cache/github-copilot-sdk/cli -type f -name copilot -print -quit)" && \
    test -n "$cli_path" && \
    install -m 0755 "$cli_path" /usr/local/bin/copilot && \
    rm -rf /root/.cache/github-copilot-sdk

ENV COPILOT_CLI_PATH=/usr/local/bin/copilot
ENV PORT=8088
ENV HOST=0.0.0.0
ENV PYTHONUNBUFFERED=1

EXPOSE 8088

# Precompile dependencies best-effort, but fail the build for invalid
# application Python.
RUN PYTHONDONTWRITEBYTECODE= python -m compileall -q $(python -c "import sysconfig as s; print(s.get_paths()['stdlib'], s.get_paths()['purelib'])") || true; \
    PYTHONDONTWRITEBYTECODE= python -m compileall -q .

CMD ["python", "main.py"]
