# syntax=docker/dockerfile:1.7
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS restore
WORKDIR /source
COPY Directory.Packages.props EcommerceApp.sln ./
COPY src/EcommerceApp.Web/EcommerceApp.Web.csproj src/EcommerceApp.Web/
COPY tests/EcommerceApp.UnitTests/EcommerceApp.UnitTests.csproj tests/EcommerceApp.UnitTests/
COPY tests/EcommerceApp.IntegrationTests/EcommerceApp.IntegrationTests.csproj tests/EcommerceApp.IntegrationTests/
RUN dotnet restore EcommerceApp.sln

FROM restore AS build
COPY . .
RUN dotnet build EcommerceApp.sln --no-restore -c Release

FROM build AS test
ENTRYPOINT ["dotnet", "test", "EcommerceApp.sln", "--no-build", "-c", "Release", "--logger", "console;verbosity=normal"]

FROM build AS publish
RUN dotnet publish src/EcommerceApp.Web/EcommerceApp.Web.csproj --no-build -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=publish --chown=1654:1654 /app/publish .
USER 1654
ENV ASPNETCORE_URLS=http://+:8080 DOTNET_EnableDiagnostics=0
EXPOSE 8080
ENTRYPOINT ["dotnet", "EcommerceApp.Web.dll"]
