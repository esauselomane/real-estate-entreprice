FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["backend-services/BrochureGeneration.Worker/BrochureGeneration.Worker.csproj", "backend-services/BrochureGeneration.Worker/"]
COPY ["backend-services/BrochureGeneration.Worker/appsettings.json", "backend-services/BrochureGeneration.Worker/"]
COPY ["backend-services/BrochureGeneration.Worker/", "backend-services/BrochureGeneration.Worker/"]

RUN dotnet restore "backend-services/BrochureGeneration.Worker/BrochureGeneration.Worker.csproj"
RUN dotnet build "backend-services/BrochureGeneration.Worker/BrochureGeneration.Worker.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "backend-services/BrochureGeneration.Worker/BrochureGeneration.Worker.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BrochureGeneration.Worker.dll"]
