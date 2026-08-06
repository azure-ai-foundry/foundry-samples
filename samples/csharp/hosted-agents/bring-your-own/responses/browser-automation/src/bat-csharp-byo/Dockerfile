FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8088
ENV ASPNETCORE_URLS=http://+:8088

# Install Node.js and @playwright/cli
RUN apt-get update && apt-get install -y curl && \
    curl -fsSL https://deb.nodesource.com/setup_22.x | bash - && \
    apt-get install -y nodejs && \
    npm install -g @playwright/cli@latest && \
    playwright-cli install --skills && \
    apt-get clean && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
# Publish ReadyToRun: precompile IL to native code for the target runtime so
# the JIT does far less work during container start. Restore/build/publish are
# collapsed into one publish because ReadyToRun must be set at restore time
# too (otherwise NETSDK1094). --self-contained false keeps the framework-
# dependent layout, so this costs image size but not a runtime dependency.
RUN dotnet publish -c Release -r linux-x64 --self-contained false -p:PublishReadyToRun=true -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "browser-automation.dll"]
