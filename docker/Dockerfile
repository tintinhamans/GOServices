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

RUN mkdir -p /app/Exceptions \
    && chown "$APP_UID:$APP_UID" /app/Exceptions

USER $APP_UID

EXPOSE 9001

ENTRYPOINT ["dotnet", "GenOnlineService.dll"]
