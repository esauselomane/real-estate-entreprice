FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["backend-services/Shared.Access/", "backend-services/Shared.Access/"]
COPY ["backend-services/Shared.Presentation/", "backend-services/Shared.Presentation/"]
COPY ["frontend-client/public/logo.jpg", "frontend-client/public/logo.jpg"]

COPY ["backend-services/Identity.Microservice/Identity.Microservice.csproj", "backend-services/Identity.Microservice/"]
COPY ["backend-services/Identity.Microservice/appsettings.json", "backend-services/Identity.Microservice/"]
COPY ["backend-services/Identity.Microservice/", "backend-services/Identity.Microservice/"]

RUN dotnet restore "backend-services/Identity.Microservice/Identity.Microservice.csproj"
RUN dotnet build "backend-services/Identity.Microservice/Identity.Microservice.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "backend-services/Identity.Microservice/Identity.Microservice.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 5001

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:5001

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Identity.Microservice.dll"]
