FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["Developer.API/Developer.API.csproj", "Developer.API/"]
COPY ["Developer.API/appsettings.json", "Developer.API/"]
COPY ["Developer.API/", "Developer.API/"]

RUN dotnet restore "Developer.API/Developer.API.csproj"
RUN dotnet build "Developer.API/Developer.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Developer.API/Developer.API.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 5006

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:5006

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Developer.API.dll"]
