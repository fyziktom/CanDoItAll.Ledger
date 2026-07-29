# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0.201 AS build
WORKDIR /src/CanDoItAll.Ledger
ARG CDA_COMPONENTS_PACKAGE_VERSION=0.1.4

COPY . .
RUN mkdir -p /src/CanDoItAll/ExternalPackages
RUN dotnet restore "tools/CanDoItAll.Ledger.DemoSeeder/CanDoItAll.Ledger.DemoSeeder.csproj" \
    /m:1 \
    /p:EnableBusinessObjectIpfsStorage=false \
    /p:CanDoItAllPackageBaseVersion="$CDA_COMPONENTS_PACKAGE_VERSION" \
    /p:CanDoItAllComponentsPackageVersion="$CDA_COMPONENTS_PACKAGE_VERSION" \
    /p:Version="$CDA_COMPONENTS_PACKAGE_VERSION" \
    /p:PackageVersion="$CDA_COMPONENTS_PACKAGE_VERSION"

FROM build AS publish
RUN dotnet publish "tools/CanDoItAll.Ledger.DemoSeeder/CanDoItAll.Ledger.DemoSeeder.csproj" \
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

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app

ENV DOTNET_EnableDiagnostics=0

USER root
RUN mkdir -p /var/lib/candoitall-ledger-demo/keys \
    && chown -R "$APP_UID:$APP_UID" /var/lib/candoitall-ledger-demo

COPY --from=publish /app/publish .

USER $APP_UID
ENTRYPOINT ["dotnet", "CanDoItAll.Ledger.DemoSeeder.dll"]
