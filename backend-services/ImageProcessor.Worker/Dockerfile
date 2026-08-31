FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["backend-services/ImageProcessor.Worker/ImageProcessor.Worker.csproj", "backend-services/ImageProcessor.Worker/"]
COPY ["backend-services/ImageProcessor.Worker/Worker.cs", "backend-services/ImageProcessor.Worker/"]
COPY ["backend-services/ImageProcessor.Worker/Program.cs", "backend-services/ImageProcessor.Worker/"]

RUN dotnet restore "backend-services/ImageProcessor.Worker/ImageProcessor.Worker.csproj"
RUN dotnet build "backend-services/ImageProcessor.Worker/ImageProcessor.Worker.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "backend-services/ImageProcessor.Worker/ImageProcessor.Worker.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ImageProcessor.Worker.dll"]
