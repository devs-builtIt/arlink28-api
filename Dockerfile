FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["Arlink28.Api.csproj", "./"]
RUN dotnet restore

COPY . .
# The repo root holds both the project and a solution file, so the project must be named.
RUN dotnet publish "Arlink28.Api.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# The image runs as the non-root `app` user, so everything it writes must be owned by it:
# /logs (Serilog file sink), /data/media (uploaded photos when MediaStorage is LocalDisk with
# RootPath=/data/media; mount a volume there) and the default local-storage folder.
RUN mkdir -p /app/local-storage/media /data/media /logs \
    && chown -R app:app /app/local-storage /data /logs

COPY --from=build --chown=app:app /app/publish .

ENV ASPNETCORE_URLS="http://+:${PORT:-8080}"
EXPOSE 8080

USER app
ENTRYPOINT ["dotnet", "Arlink28.Api.dll"]
