FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["Arlink28.Api.csproj", "./"]
RUN dotnet restore

COPY . .
# The repo root holds both the project and a solution file, so the project must be named.
RUN dotnet publish "Arlink28.Api.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN mkdir -p /app/local-storage/media /logs

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS="http://+:${PORT:-8080}"
EXPOSE 8080

ENTRYPOINT ["dotnet", "Arlink28.Api.dll"]
