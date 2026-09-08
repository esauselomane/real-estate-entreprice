FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["backend-services/PlatformSyndication.Worker/PlatformSyndication.Worker.csproj", "backend-services/PlatformSyndication.Worker/"]
COPY ["backend-services/PlatformSyndication.Worker/appsettings.json", "backend-services/PlatformSyndication.Worker/"]
COPY ["backend-services/Shared.Kafka/Shared.Kafka.csproj", "backend-services/Shared.Kafka/"]

RUN dotnet restore "backend-services/PlatformSyndication.Worker/PlatformSyndication.Worker.csproj"

COPY ["backend-services/Shared.Kafka/", "backend-services/Shared.Kafka/"]
COPY ["backend-services/PlatformSyndication.Worker/", "backend-services/PlatformSyndication.Worker/"]

RUN dotnet build "backend-services/PlatformSyndication.Worker/PlatformSyndication.Worker.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "backend-services/PlatformSyndication.Worker/PlatformSyndication.Worker.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PlatformSyndication.Worker.dll"]
