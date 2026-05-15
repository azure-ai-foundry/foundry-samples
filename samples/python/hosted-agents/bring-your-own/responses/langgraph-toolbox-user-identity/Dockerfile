FROM mcr.microsoft.com/azurelinux/base/python:3.12
WORKDIR /app
COPY . user_agent/
WORKDIR /app/user_agent
# Upgrade pip first: the rpm-installed pip 24.2 in this base image cannot parse
# newer dependency markers (e.g. extra == "contentunderstanding" on
# langchain-azure-ai 1.2.3+). --ignore-installed avoids the
# "uninstall-no-record-file" error from the rpm-managed pip package.
RUN pip install --upgrade --ignore-installed pip
RUN if [ -f requirements.txt ]; then pip install -r requirements.txt; fi
EXPOSE 8088
CMD ["python3", "main.py"]
