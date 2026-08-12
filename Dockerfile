# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore separately so dependency downloads remain cached when source files change.
COPY GenOnlineService/GenOnlineService.csproj GenOnlineService/
RUN dotnet restore GenOnlineService/GenOnlineService.csproj

COPY GenOnlineService/ GenOnlineService/
RUN dotnet publish GenOnlineService/GenOnlineService.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

# Direct runs retain the HTTPS endpoint from appsettings.json. The image uses
# the HTTP endpoint so TLS can be terminated outside the application.
RUN sed -i '/^[[:space:]]*"HTTPS": {$/,/^[[:space:]]*},$/d' /app/publish/appsettings.json

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Listen on the service's default HTTP port. A mounted appsettings.json controls
# whether forwarded headers are trusted for the deployment.
ENV Kestrel__Endpoints__HTTP__Url=http://0.0.0.0:5000

COPY --from=build /app/publish/ ./

# Some runtime data files are not included by the project publish rules.
COPY --from=build /src/GenOnlineService/data/ ./data/

# Override /app/appsettings.json with a read-only bind mount for deployment
# configuration and secrets.

# The service writes crash reports here. Keep the rest of the image read-only-capable.
RUN mkdir -p /app/Exceptions && chown -R "$APP_UID:$APP_UID" /app/Exceptions

USER $APP_UID

EXPOSE 5000

ENTRYPOINT ["dotnet", "GenOnlineService.dll"]
