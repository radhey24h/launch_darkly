# ============================================================
# Gateway Dockerfile
# Multi-stage build: small final image, fast layer caching.
# ============================================================

# --- Stage 1: Restore & Build ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first (layer cache: only re-runs restore when .csproj changes)
COPY src/Gateway/Gateway.csproj src/Gateway/
RUN dotnet restore src/Gateway/Gateway.csproj

# Copy full source and publish
COPY src/Gateway/ src/Gateway/
WORKDIR /src/src/Gateway
RUN dotnet publish Gateway.csproj -c Release -o /app/publish

# --- Stage 2: Runtime image ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install curl so the docker-compose healthcheck (curl /health) works.
# The dotnet/aspnet base image does not include curl by default.
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

# Copy published output from build stage
COPY --from=build /app/publish .

# Gateway listens on port 5000
EXPOSE 5000

# Set production environment
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:5000

# The LaunchDarkly SDK key MUST be injected at runtime.
# Pass it as an environment variable in docker-compose or your orchestrator:
#   -e LaunchDarkly__SdkKey=sdk-your-key-here
ENV LaunchDarkly__SdkKey=""

ENTRYPOINT ["dotnet", "Gateway.dll"]
