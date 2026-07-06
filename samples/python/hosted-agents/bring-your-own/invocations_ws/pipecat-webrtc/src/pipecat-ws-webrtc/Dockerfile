FROM python:3.12-slim

WORKDIR /app

# Prevent .pyc files (saves ~213MB)
ENV PYTHONDONTWRITEBYTECODE=1

# Foundry hosted agents expose port 8088 by convention; force the
# server to bind there regardless of the .env default (8089).
ENV SERVER_PORT=8088

# Install Python dependencies and strip unnecessary artifacts in the same layer.
# After the resolver runs, force-uninstall the GUI variant of opencv-python
# (pulled in transitively by pipecat[webrtc]) and reinstall the headless variant
# so the slim base image doesn't need libxcb / libGL. Both packages share the
# same `cv2/` directory, so the uninstall step deletes it — we must
# `--force-reinstall` headless to recreate it.
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt \
    && pip uninstall -y opencv-python opencv-contrib-python || true \
    && pip install --no-cache-dir --force-reinstall --no-deps opencv-python-headless \
    && pip uninstall -y pip setuptools \
    && find /usr/local/lib/python3.12/site-packages \
        \( -type d -name '__pycache__' \
        -o -type d -name 'tests' \
        -o -type d -name 'test' \) \
        -exec rm -rf {} + 2>/dev/null; true

# Copy application code. bot_websocket_server.py and the azure_* helpers are
# shared with the sibling pipecat-ws-server sample and are bundled here so
# the container is self-contained (the runtime sys.path insertion in
# bot_webrtc_server.py is harmless when these files live next to it).
COPY server.py \
     bot_webrtc_server.py \
     bot_websocket_server.py \
     azure_fast_transcription_stt.py \
     azure_tts_text_streaming.py \
     ./

EXPOSE 8088

CMD ["python", "server.py"]
