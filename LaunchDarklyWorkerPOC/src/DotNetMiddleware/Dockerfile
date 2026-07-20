FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/Shared/LaunchDarklyPOC.Shared/LaunchDarklyPOC.Shared.csproj",             "src/Shared/LaunchDarklyPOC.Shared/"]
COPY ["src/DotNetMiddleware/LaunchDarklyPOC.DotNetMiddleware.csproj",                  "src/DotNetMiddleware/"]
RUN dotnet restore "src/DotNetMiddleware/LaunchDarklyPOC.DotNetMiddleware.csproj"

COPY src/Shared/          src/Shared/
COPY src/DotNetMiddleware/ src/DotNetMiddleware/

WORKDIR "/src/src/DotNetMiddleware"
RUN dotnet publish "LaunchDarklyPOC.DotNetMiddleware.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "LaunchDarklyPOC.DotNetMiddleware.dll"]
