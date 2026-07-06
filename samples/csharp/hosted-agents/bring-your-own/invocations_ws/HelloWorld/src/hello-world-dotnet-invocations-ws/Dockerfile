FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src
COPY HelloWorld.csproj ./
RUN dotnet restore HelloWorld.csproj
COPY Program.cs ./
RUN dotnet publish HelloWorld.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app
COPY --from=build /app .
EXPOSE 8088
ENTRYPOINT ["dotnet", "HelloWorld.dll"]
