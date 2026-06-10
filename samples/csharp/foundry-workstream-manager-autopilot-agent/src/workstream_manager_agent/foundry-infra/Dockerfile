FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

ARG BLUEPRINT_CLIENT_ID
ARG AUTHORITY_ENDPOINT
ARG AZURE_OPENAI_ENDPOINT
ARG MODEL_DEPLOYMENT
ARG PROJECT_DEFAULT_INSTANCE_CLIENT_ID
ARG WORK_ITEMS_TABLE_SERVICE_URI
ARG WORK_ITEMS_TABLE_NAME
COPY ./publish .

# Expose the default port
EXPOSE 8088

ENV ASPNETCORE_URLS=http://+:8088

ENV MCP_PLATFORM_ENDPOINT=https://agent365.svc.cloud.microsoft

ENV Connections__ServiceConnection__Settings__ClientId=${BLUEPRINT_CLIENT_ID}
ENV Connections__ServiceConnection__Settings__AuthorityEndpoint=${AUTHORITY_ENDPOINT}
ENV AzureOpenAIEndpoint=${AZURE_OPENAI_ENDPOINT}
ENV ModelDeployment=${MODEL_DEPLOYMENT}
ENV ProjectDefaultInstanceClientId=${PROJECT_DEFAULT_INSTANCE_CLIENT_ID}
ENV WorkItemsTableServiceUri=${WORK_ITEMS_TABLE_SERVICE_URI}
ENV WorkItemsTableName=${WORK_ITEMS_TABLE_NAME}

# Start the application
ENTRYPOINT ["dotnet", "WorkstreamManagerAgent.dll"]

