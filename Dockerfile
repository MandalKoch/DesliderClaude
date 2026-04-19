# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY DesliderClaude.slnx ./
COPY src/DesliderClaude.Core/DesliderClaude.Core.csproj           src/DesliderClaude.Core/
COPY src/DesliderClaude.Data/DesliderClaude.Data.csproj           src/DesliderClaude.Data/
COPY src/DesliderClaude.ServiceDefaults/DesliderClaude.ServiceDefaults.csproj src/DesliderClaude.ServiceDefaults/
COPY src/DesliderClaude.Web/DesliderClaude.Web.csproj             src/DesliderClaude.Web/
COPY src/DesliderClaude.Web.Client/DesliderClaude.Web.Client.csproj src/DesliderClaude.Web.Client/

RUN dotnet restore src/DesliderClaude.Web/DesliderClaude.Web.csproj

COPY src/DesliderClaude.Core/           src/DesliderClaude.Core/
COPY src/DesliderClaude.Data/           src/DesliderClaude.Data/
COPY src/DesliderClaude.ServiceDefaults/ src/DesliderClaude.ServiceDefaults/
COPY src/DesliderClaude.Web/            src/DesliderClaude.Web/
COPY src/DesliderClaude.Web.Client/     src/DesliderClaude.Web.Client/

RUN dotnet publish src/DesliderClaude.Web/DesliderClaude.Web.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_FORWARDEDHEADERS_ENABLED=true \
    DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 8080
ENTRYPOINT ["dotnet", "DesliderClaude.Web.dll"]
