FROM python:3.13-slim

WORKDIR /app

COPY ./ .

RUN pip install --upgrade pip && \
    pip install --no-cache-dir -r requirements.txt

EXPOSE 8088

CMD ["python", "main.py"]
