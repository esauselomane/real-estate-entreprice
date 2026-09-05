FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["backend-services/Identity.Microservice/Identity.Microservice.csproj", "backend-services/Identity.Microservice/"]
COPY ["backend-services/Identity.Microservice/Data/ IdentitySeedData.cs", "backend-services/Identity.Microservice/Data/"]
COPY ["backend-services/Identity.Microservice/Controllers/", "backend-services/Identity.Microservice/Controllers/"]
COPY ["backend-services/Identity.Microservice/Models/", "backend-services/Identity.Microservice/Models/"]
COPY ["backend-services/Identity.Microservice/Services/", "backend-services/Identity.Microservice/Services/"]
COPY ["backend-services/Identity.Microservice/Program.cs", "backend-services/Identity.Microservice/"]

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
