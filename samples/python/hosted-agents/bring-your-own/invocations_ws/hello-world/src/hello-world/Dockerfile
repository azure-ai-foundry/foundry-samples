FROM python:3.12-slim

WORKDIR /app

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1

COPY requirements.txt ./
RUN pip install --no-cache-dir -r requirements.txt

COPY main.py ./

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

CMD ["python", "main.py"]
