FROM python:3.12-slim

WORKDIR /app

COPY . user_agent/
WORKDIR /app/user_agent

RUN pip install --no-input --upgrade pip && \
    if [ -f requirements.txt ]; then \
    pip install --no-input -r requirements.txt; \
    else \
    echo "No requirements.txt found"; \
    fi

EXPOSE 8088

CMD ["python", "main.py"]

