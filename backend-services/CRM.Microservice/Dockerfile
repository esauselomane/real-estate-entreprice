FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["backend-services/CRM.Microservice/CRM.Microservice.csproj", "backend-services/CRM.Microservice/"]
COPY ["backend-services/CRM.Microservice/appsettings.json", "backend-services/CRM.Microservice/"]
COPY ["backend-services/CRM.Microservice/", "backend-services/CRM.Microservice/"]

RUN dotnet restore "backend-services/CRM.Microservice/CRM.Microservice.csproj"
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
