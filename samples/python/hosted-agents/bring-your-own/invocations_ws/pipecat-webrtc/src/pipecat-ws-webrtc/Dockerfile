FROM python:3.12-slim

WORKDIR /app

# Keep bytecode out of the image layer that pip writes, then precompile
# deliberately below once the app code is in place.
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
        \( -type d -name 'tests' \
        -o -type d -name 'test' \) \
        -exec rm -rf {} + 2>/dev/null; true

# Pre-download the NLTK 'punkt_tab' tokenizer data at build time.
#
# pipecat.utils.string runs `nltk.data.find("tokenizers/punkt_tab")` at *import*
# time and falls back to `nltk.download(...)` when it is missing. Because pip
# does not ship NLTK corpora, that lookup misses in a stock image and every
# cold start pays a network round trip before the module finishes importing.
#
# Baking the data in makes the lookup a local hit. The redundant .zip is
# removed because NLTK reads the extracted directory when both are present.
#
# NLTK_DATA is set explicitly rather than relying on /usr/local/share/nltk_data
# happening to be on NLTK's default search path.
ENV NLTK_DATA=/usr/local/share/nltk_data
RUN python -m nltk.downloader -d "$NLTK_DATA" punkt_tab \
    && find "$NLTK_DATA" -name '*.zip' -delete

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

CMD ["python", "server.py"]
