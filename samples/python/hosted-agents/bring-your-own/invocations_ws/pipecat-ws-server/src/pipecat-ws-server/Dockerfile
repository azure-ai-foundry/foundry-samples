FROM python:3.12-slim

WORKDIR /app

# Keep bytecode out of the image layer that pip writes, then precompile
# deliberately below once the app code is in place.
ENV PYTHONDONTWRITEBYTECODE=1

# Install Python dependencies and strip unnecessary artifacts in the same layer
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt \
    && pip uninstall -y pip setuptools \
    && find /usr/local/lib/python3.12/site-packages \
        \( -type d -name 'tests' \
        -o -type d -name 'test' \) \
        -exec rm -rf {} + 2>/dev/null; true

# Copy application code
COPY server.py bot_websocket_server.py azure_fast_transcription_stt.py azure_tts_text_streaming.py ./

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

CMD ["python", "server.py"]
