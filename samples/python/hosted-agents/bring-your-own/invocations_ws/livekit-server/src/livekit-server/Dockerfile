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
        \( -type d -name '__pycache__' \
        -o -type d -name 'tests' \
        -o -type d -name 'test' \) \
        -exec rm -rf {} + 2>/dev/null; true

# Pre-fetch Silero VAD weights so the agent doesn't download them at
# every cold-start.
RUN python -c "from livekit.plugins.silero import VAD; VAD.load()"

COPY agent.py server.py azure_tts_text_streaming.py ./

EXPOSE 8088

# server.py spawns `agent.py start` as a child process, so a single
# command runs both the FastAPI signaling endpoint and the LiveKit
# agent worker.
CMD ["python", "server.py"]
