FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY backend-services/ backend-services/
COPY Developer.API/ Developer.API/
COPY tools/PublicSite.ProductionSetup/ tools/PublicSite.ProductionSetup/
COPY frontend-client/public/logo.jpg frontend-client/public/logo.jpg

RUN dotnet restore "Developer.API/Developer.API.csproj"
RUN dotnet build "Developer.API/Developer.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Developer.API/Developer.API.csproj" -c Release -o /app/publish
COPY Developer.API/appsettings.json /app/publish/appsettings.json
RUN dotnet publish "tools/PublicSite.ProductionSetup/PublicSite.ProductionSetup.csproj" -c Release -o /app/setup

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 5006

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:5006

COPY --from=publish /app/publish .
COPY --from=publish /app/setup /app/setup
ENTRYPOINT ["dotnet", "Developer.API.dll"]
