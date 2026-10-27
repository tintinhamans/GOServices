# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY GenOnlineService/GenOnlineService.csproj GenOnlineService/
RUN dotnet restore GenOnlineService/GenOnlineService.csproj

COPY GenOnlineService/ GenOnlineService/
RUN dotnet publish GenOnlineService/GenOnlineService.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish/ ./

# Crash dumps are written to Exceptions/ (Program.cs); the root filesystem may be read-only, so the directory
# must exist and belong to the non-root user. Hosts mount data/, crcfiles/, Exceptions/ and appsettings.json.
RUN mkdir -p /app/Exceptions /app/crcfiles \
    && chown "$APP_UID:$APP_UID" /app/Exceptions

USER $APP_UID

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "GenOnlineService.dll"]
