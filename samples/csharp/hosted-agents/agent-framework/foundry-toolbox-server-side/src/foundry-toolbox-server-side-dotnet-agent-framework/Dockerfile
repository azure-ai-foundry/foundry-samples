FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src
COPY . .
# Publish ReadyToRun: precompile IL to native code for the target runtime so
# the JIT does far less work during container start. Restore/build/publish are
# collapsed into one publish because ReadyToRun must be set at restore time
# too (otherwise NETSDK1094). --self-contained false keeps the framework-
# dependent layout, so this costs image size but not a runtime dependency.
RUN dotnet publish -c Release -r linux-musl-x64 --self-contained false -p:PublishReadyToRun=true -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app
COPY --from=build /app .
EXPOSE 8088
ENTRYPOINT ["dotnet", "foundry-toolbox-server-side.dll"]
