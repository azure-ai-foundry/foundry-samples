FROM python:3.12-slim

WORKDIR /app

COPY . user_agent/
WORKDIR /app/user_agent

RUN pip install --no-input --upgrade pip && \
    if [ -f requirements-pypi.txt ]; then \
        pip install --no-input -r requirements-pypi.txt; \
    fi && \
    if [ -f requirements.txt ]; then \
        pip install --no-input -r requirements.txt; \
    else \
        echo "No requirements.txt found"; \
    fi

EXPOSE 8088

# vNext (ADC): Append the egress proxy CA cert to system and Python cert bundles
# so that outbound HTTPS (App Insights, Azure services, etc.) works correctly.
CMD bash -c '\
  if [ -f /etc/ssl/certs/adc-egress-proxy-ca.crt ]; then \
    cat /etc/ssl/certs/adc-egress-proxy-ca.crt >> /etc/ssl/certs/ca-certificates.crt && \
    cat /etc/ssl/certs/adc-egress-proxy-ca.crt >> $(python -c "import certifi; print(certifi.where())"); \
  fi && \
  python main.py'
