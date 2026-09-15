FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["backend-services/Shared.Access/", "backend-services/Shared.Access/"]

COPY ["backend-services/Property.Microservice/Property.Microservice.csproj", "backend-services/Property.Microservice/"]
COPY ["backend-services/Property.Microservice/appsettings.json", "backend-services/Property.Microservice/"]
COPY ["backend-services/Shared.Kafka/Shared.Kafka.csproj", "backend-services/Shared.Kafka/"]
COPY ["backend-services/Property.Microservice/", "backend-services/Property.Microservice/"]

RUN dotnet restore "backend-services/Property.Microservice/Property.Microservice.csproj"
COPY ["backend-services/Shared.Kafka/", "backend-services/Shared.Kafka/"]
RUN dotnet build "backend-services/Property.Microservice/Property.Microservice.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "backend-services/Property.Microservice/Property.Microservice.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 5002

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:5002

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Property.Microservice.dll"]
