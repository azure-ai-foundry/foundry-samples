FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

COPY ./publish .

# Expose the default port
EXPOSE 8088

ENV ASPNETCORE_URLS=http://+:8088

ENV MCP_PLATFORM_ENDPOINT=https://agent365.svc.cloud.microsoft

# Start the application
ENTRYPOINT ["dotnet", "HelloWorldA365Agent.dll"]
