FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

ARG BLUEPRINT_CLIENT_ID
ARG AUTHORITY_ENDPOINT
ARG AZURE_OPENAI_ENDPOINT
ARG MODEL_DEPLOYMENT
COPY ./publish .

# Expose the default port
EXPOSE 8088

ENV ASPNETCORE_URLS=http://+:8088

ENV MCP_PLATFORM_ENDPOINT=https://agent365.svc.cloud.microsoft

ENV Connections__ServiceConnection__Settings__ClientId=${BLUEPRINT_CLIENT_ID}
ENV Connections__ServiceConnection__Settings__AuthorityEndpoint=${AUTHORITY_ENDPOINT}
ENV AzureOpenAIEndpoint=${AZURE_OPENAI_ENDPOINT}
ENV ModelDeployment=${MODEL_DEPLOYMENT}

# Start the application
ENTRYPOINT ["dotnet", "HelloWorldA365Agent.dll"]
