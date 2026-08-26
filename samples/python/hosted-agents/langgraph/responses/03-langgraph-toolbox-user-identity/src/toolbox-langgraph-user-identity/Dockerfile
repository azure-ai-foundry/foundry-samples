FROM python:3.13-slim
WORKDIR /app
COPY . user_agent/
WORKDIR /app/user_agent
RUN pip install --upgrade pip && if [ -f requirements.txt ]; then pip install -r requirements.txt; fi
EXPOSE 8088

# Precompile Python bytecode at build time so cold starts don't pay for it.
# Dependencies are best-effort so vendored files cannot fail the build;
# application code is compiled strictly so syntax errors surface at build time.
RUN PYTHONDONTWRITEBYTECODE= python -m compileall -q $(python -c "import sysconfig as s; print(s.get_paths()['stdlib'], s.get_paths()['purelib'])") || true; \
    PYTHONDONTWRITEBYTECODE= python -m compileall -q .

CMD ["python", "main.py"]
