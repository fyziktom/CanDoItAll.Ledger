# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0.201 AS build
WORKDIR /src/CanDoItAll.Ledger
ARG CDA_COMPONENTS_PACKAGE_VERSION=0.1.4

COPY . .
COPY --from=candoitall-components . /src/CanDoItAll.Components
RUN sh docker/prepare-component-feed.sh "$CDA_COMPONENTS_PACKAGE_VERSION"
RUN dotnet restore "examples/CanDoItAll.Ledger.AccountApp/CanDoItAll.Ledger.AccountApp.csproj" \
    /m:1 \
    /p:EnableBusinessObjectIpfsStorage=false \
    /p:CanDoItAllPackageBaseVersion="$CDA_COMPONENTS_PACKAGE_VERSION" \
    /p:CanDoItAllComponentsPackageVersion="$CDA_COMPONENTS_PACKAGE_VERSION" \
    /p:Version="$CDA_COMPONENTS_PACKAGE_VERSION" \
    /p:PackageVersion="$CDA_COMPONENTS_PACKAGE_VERSION"

FROM build AS publish
RUN dotnet publish "examples/CanDoItAll.Ledger.AccountApp/CanDoItAll.Ledger.AccountApp.csproj" \
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

FROM nginxinc/nginx-unprivileged:1.27-alpine AS final
COPY --chown=101:101 docker/accountapp/nginx.conf /etc/nginx/conf.d/default.conf
COPY --chown=101:101 docker/accountapp/docker-entrypoint-accountapp.sh /docker-entrypoint.d/30-accountapp-config.sh
RUN chmod +x /docker-entrypoint.d/30-accountapp-config.sh
COPY --chown=101:101 --from=publish /app/publish/wwwroot /usr/share/nginx/html

USER 101
