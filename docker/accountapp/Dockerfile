# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0.302 AS build
WORKDIR /src/CanDoItAll.Economy
ARG CDA_COMPONENTS_PACKAGE_VERSION=0.1.4

COPY . .
COPY --from=candoitall-components . /src/CanDoItAll.Components
COPY --from=candoitall-external-packages . /src/CanDoItAll/ExternalPackages
RUN dotnet restore "examples/CanDoItAll.Economy.AccountApp/CanDoItAll.Economy.AccountApp.csproj" \
    /m:1 \
    /p:EnableBusinessObjectIpfsStorage=false \
    /p:CanDoItAllPackageBaseVersion="$CDA_COMPONENTS_PACKAGE_VERSION" \
    /p:CanDoItAllComponentsPackageVersion="$CDA_COMPONENTS_PACKAGE_VERSION" \
    /p:Version="$CDA_COMPONENTS_PACKAGE_VERSION" \
    /p:PackageVersion="$CDA_COMPONENTS_PACKAGE_VERSION"

FROM build AS publish
RUN dotnet publish "examples/CanDoItAll.Economy.AccountApp/CanDoItAll.Economy.AccountApp.csproj" \
    --configuration Release \
    --no-restore \
    /m:1 \
    --output /app/publish \
    /p:UseAppHost=false \
    /p:EnableBusinessObjectIpfsStorage=false \
    /p:CanDoItAllPackageBaseVersion="$CDA_COMPONENTS_PACKAGE_VERSION" \
    /p:CanDoItAllComponentsPackageVersion="$CDA_COMPONENTS_PACKAGE_VERSION" \
    /p:Version="$CDA_COMPONENTS_PACKAGE_VERSION" \
    /p:PackageVersion="$CDA_COMPONENTS_PACKAGE_VERSION"

FROM nginx:1.27-alpine AS final
COPY docker/accountapp/nginx.conf /etc/nginx/conf.d/default.conf
COPY docker/accountapp/docker-entrypoint-accountapp.sh /docker-entrypoint.d/30-accountapp-config.sh
RUN chmod +x /docker-entrypoint.d/30-accountapp-config.sh
COPY --from=publish /app/publish/wwwroot /usr/share/nginx/html
