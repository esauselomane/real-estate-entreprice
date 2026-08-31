FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["backend-services/Property.Microservice/Property.Microservice.csproj", "backend-services/Property.Microservice/"]
COPY ["backend-services/Property.Microservice/Data/ PropertyDbContext.cs", "backend-services/Property.Microservice/Data/"]
COPY ["backend-services/Property.Microservice/Controllers/", "backend-services/Property.Microservice/Controllers/"]
COPY ["backend-services/Property.Microservice/Models/", "backend-services/Property.Microservice/Models/"]
COPY ["backend-services/Property.Microservice/Services/", "backend-services/Property.Microservice/Services/"]
COPY ["backend-services/Property.Microservice/Program.cs", "backend-services/Property.Microservice/"]

RUN dotnet restore "backend-services/Property.Microservice/Property.Microservice.csproj"
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
