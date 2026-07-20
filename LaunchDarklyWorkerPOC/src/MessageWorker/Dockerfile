FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/Shared/LaunchDarklyPOC.Shared/LaunchDarklyPOC.Shared.csproj",       "src/Shared/LaunchDarklyPOC.Shared/"]
COPY ["src/MessageWorker/LaunchDarklyPOC.MessageWorker.csproj",                  "src/MessageWorker/"]
RUN dotnet restore "src/MessageWorker/LaunchDarklyPOC.MessageWorker.csproj"

COPY src/Shared/      src/Shared/
COPY src/MessageWorker/ src/MessageWorker/

WORKDIR "/src/src/MessageWorker"
RUN dotnet publish "LaunchDarklyPOC.MessageWorker.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "LaunchDarklyPOC.MessageWorker.dll"]
