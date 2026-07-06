FROM python:3.12-slim

WORKDIR /app

# Prevent .pyc files (saves ~213MB)
ENV PYTHONDONTWRITEBYTECODE=1

# Install Python dependencies and strip unnecessary artifacts in the same layer
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt \
    && pip uninstall -y pip setuptools \
    && find /usr/local/lib/python3.12/site-packages \
        \( -type d -name '__pycache__' \
        -o -type d -name 'tests' \
        -o -type d -name 'test' \) \
        -exec rm -rf {} + 2>/dev/null; true

# Copy application code
COPY server.py bot_websocket_server.py azure_fast_transcription_stt.py azure_tts_text_streaming.py ./

EXPOSE 8088

CMD ["python", "server.py"]
