# ============================================================
# DotNetBackend Dockerfile
# ============================================================

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/DotNetBackend/DotNetBackend.csproj src/DotNetBackend/
RUN dotnet restore src/DotNetBackend/DotNetBackend.csproj

COPY src/DotNetBackend/ src/DotNetBackend/
WORKDIR /src/src/DotNetBackend
RUN dotnet publish DotNetBackend.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install curl so the docker-compose healthcheck (curl /health) works.
# The dotnet/aspnet base image does not include curl by default.
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

EXPOSE 5001

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:5001

ENTRYPOINT ["dotnet", "DotNetBackend.dll"]
