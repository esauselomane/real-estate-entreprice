FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["backend-services/DataImport.Worker/DataImport.Worker.csproj", "backend-services/DataImport.Worker/"]
COPY ["backend-services/DataImport.Worker/appsettings.json", "backend-services/DataImport.Worker/"]
COPY ["backend-services/DataImport.Worker/", "backend-services/DataImport.Worker/"]

RUN dotnet restore "backend-services/DataImport.Worker/DataImport.Worker.csproj"
RUN dotnet build "backend-services/DataImport.Worker/DataImport.Worker.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "backend-services/DataImport.Worker/DataImport.Worker.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DataImport.Worker.dll"]
