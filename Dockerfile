# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0-windowsservercore-ltsc2022 AS build
WORKDIR /src

# Copy csproj and restore
COPY ["LombardAPI_NEW/LombardAPI_NEW.csproj", "LombardAPI_NEW/"]
RUN dotnet restore "LombardAPI_NEW/LombardAPI_NEW.csproj"

# Copy remaining files and publish
COPY . .
WORKDIR /src/LombardAPI_NEW
RUN dotnet publish "LombardAPI_NEW.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0-windowsservercore-ltsc2022 AS runtime
WORKDIR /app

# Configure ASP.NET
ENV ASPNETCORE_URLS=http://+:80
ENV ASPNETCORE_ENVIRONMENT=Production

# Copy published output
COPY --from=build /app/publish .

EXPOSE 80

ENTRYPOINT ["dotnet", "LombardAPI_NEW.dll"]
