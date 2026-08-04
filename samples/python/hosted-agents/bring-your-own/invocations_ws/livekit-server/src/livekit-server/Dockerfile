FROM python:3.12-slim

WORKDIR /app

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1

RUN apt-get update && apt-get install -y --no-install-recommends \
    gcc g++ && rm -rf /var/lib/apt/lists/*

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt \
    && pip uninstall -y pip setuptools \
    && find /usr/local/lib/python3.12/site-packages \
        \( -type d -name 'tests' \
        -o -type d -name 'test' \) \
        -exec rm -rf {} + 2>/dev/null; true

# Pre-fetch Silero VAD weights so the agent doesn't download them at
# every cold-start.
RUN python -c "from livekit.plugins.silero import VAD; VAD.load()"

COPY agent.py server.py azure_tts_text_streaming.py ./

# Precompile to bytecode at build time so every cold start doesn't have to.
# PYTHONDONTWRITEBYTECODE blocks bytecode *writes*, not *reads*, so the flag
# set above still prevents runtime .pyc churn while these prebuilt
# __pycache__ directories are used.
#
# Dependencies are compiled best-effort (`|| true`): a stray unparsable file
# vendored by a third-party package shouldn't fail the build, and Python just
# falls back to compiling it at import. Application code is compiled strictly,
# so a syntax error in this sample fails the build rather than the container.
RUN PYTHONDONTWRITEBYTECODE= python -m compileall -q /usr/local/lib/python3.12/site-packages || true; \
    PYTHONDONTWRITEBYTECODE= python -m compileall -q /app

EXPOSE 8088

# server.py spawns `agent.py start` as a child process, so a single
# command runs both the FastAPI signaling endpoint and the LiveKit
# agent worker.
CMD ["python", "server.py"]
