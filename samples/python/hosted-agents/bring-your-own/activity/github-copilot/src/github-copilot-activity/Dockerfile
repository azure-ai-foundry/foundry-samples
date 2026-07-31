FROM python:3.13-slim

WORKDIR /app
COPY . user_agent/
WORKDIR /app/user_agent
RUN if [ -f requirements.txt ]; then pip install --no-input -r requirements.txt; fi
EXPOSE 8088

ENV PORT=8088
ENV HOST=0.0.0.0
ENV PYTHONUNBUFFERED=1

CMD ["python", "main.py"]