# ---------------------------------------------------------------------------
# Etapa 1: compilación
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Se copian primero los archivos de proyecto para que la restauración de paquetes
# quede cacheada y no se repita en cada cambio de código.
COPY ProductCatalog.sln ./
COPY src/ProductCatalog.Domain/*.csproj         src/ProductCatalog.Domain/
COPY src/ProductCatalog.Application/*.csproj    src/ProductCatalog.Application/
COPY src/ProductCatalog.Infrastructure/*.csproj src/ProductCatalog.Infrastructure/
COPY src/ProductCatalog.Api/*.csproj            src/ProductCatalog.Api/
COPY tests/ProductCatalog.Tests/*.csproj        tests/ProductCatalog.Tests/
RUN dotnet restore

COPY . .
RUN dotnet publish src/ProductCatalog.Api/ProductCatalog.Api.csproj \
      -c Release \
      -o /app/publish \
      --no-restore

# ---------------------------------------------------------------------------
# Etapa 2: ejecución
# ---------------------------------------------------------------------------
# Imagen sin SDK: más pequeña y con menos superficie expuesta.
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# La API se ejecuta sin privilegios de root.
USER $APP_UID

# Los proveedores de nube (Render, Railway, Fly...) inyectan el puerto en PORT.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "ProductCatalog.Api.dll"]
