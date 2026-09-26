# Etapa 1: build y publish con el SDK de .NET
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["X-Chang.API/X-Chang.API.csproj", "X-Chang.API/"]
COPY ["X-Chang.CORE/X-Chang.CORE.csproj", "X-Chang.CORE/"]
RUN dotnet restore "X-Chang.API/X-Chang.API.csproj"

COPY . .
WORKDIR /src/X-Chang.API
RUN dotnet publish -c Release -o /app/publish --no-restore

# Etapa 2: runtime liviano, solo ASP.NET
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

COPY --from=build /app/publish .
COPY X-Chang.API/appsettings.example.json ./appsettings.json

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

# La imagen base de .NET 8+ ya define un usuario sin privilegios llamado "app"
USER app

ENTRYPOINT ["dotnet", "X-Chang.API.dll"]
