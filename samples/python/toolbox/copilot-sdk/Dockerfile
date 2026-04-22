FROM mcr.microsoft.com/azurelinux/base/python:3.12
WORKDIR /app
COPY . user_agent/
WORKDIR /app/user_agent
RUN if [ -f requirements.txt ]; then pip install --pre -r requirements.txt; fi
EXPOSE 8088
CMD ["python3", "main.py"]
