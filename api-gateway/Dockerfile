FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["api-gateway/api-gateway.csproj", "api-gateway/"]
COPY ["api-gateway/appsettings.json", "api-gateway/"]
COPY ["api-gateway/ocelot.json", "api-gateway/"]
COPY ["api-gateway/", "api-gateway/"]

RUN dotnet restore "api-gateway/api-gateway.csproj"
RUN dotnet build "api-gateway/api-gateway.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "api-gateway/api-gateway.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 5000

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:5000

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "api-gateway.dll"]
