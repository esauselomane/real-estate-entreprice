FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["backend-services/Shared.Access/", "backend-services/Shared.Access/"]
COPY ["backend-services/Shared.Presentation/", "backend-services/Shared.Presentation/"]
COPY ["frontend-client/public/logo.jpg", "frontend-client/public/logo.jpg"]

COPY ["backend-services/CRM.Microservice/CRM.Microservice.csproj", "backend-services/CRM.Microservice/"]
COPY ["backend-services/CRM.Microservice/appsettings.json", "backend-services/CRM.Microservice/"]
COPY ["backend-services/Shared.Kafka/Shared.Kafka.csproj", "backend-services/Shared.Kafka/"]

COPY ["backend-services/CRM.Microservice/", "backend-services/CRM.Microservice/"]

RUN dotnet restore "backend-services/CRM.Microservice/CRM.Microservice.csproj"
COPY ["backend-services/Shared.Kafka/", "backend-services/Shared.Kafka/"]
RUN dotnet build "backend-services/CRM.Microservice/CRM.Microservice.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "backend-services/CRM.Microservice/CRM.Microservice.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 5003

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:5003

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CRM.Microservice.dll"]
