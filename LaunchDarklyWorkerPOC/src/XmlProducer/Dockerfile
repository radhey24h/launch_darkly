FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/Shared/LaunchDarklyPOC.Shared/LaunchDarklyPOC.Shared.csproj",       "src/Shared/LaunchDarklyPOC.Shared/"]
COPY ["src/XmlProducer/LaunchDarklyPOC.XmlProducer.csproj",                     "src/XmlProducer/"]
RUN dotnet restore "src/XmlProducer/LaunchDarklyPOC.XmlProducer.csproj"

COPY src/Shared/   src/Shared/
COPY src/XmlProducer/ src/XmlProducer/

WORKDIR "/src/src/XmlProducer"
RUN dotnet publish "LaunchDarklyPOC.XmlProducer.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "LaunchDarklyPOC.XmlProducer.dll"]
