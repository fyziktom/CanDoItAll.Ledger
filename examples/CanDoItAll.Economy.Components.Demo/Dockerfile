# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0.201 AS build
WORKDIR /src/CanDoItAll.Economy
ARG CDA_COMPONENTS_PACKAGE_VERSION=0.1.2

COPY . .
COPY --from=candoitall-ipfs . /src/CanDoItAll.IPFS/src
COPY --from=candoitall-components . /src/CanDoItAll.Components/src
COPY --from=candoitall-external-packages . /src/CanDoItAll/ExternalPackages
COPY docker/components/Directory.Build.props /src/CanDoItAll.Components/Directory.Build.props
RUN dotnet restore "examples/CanDoItAll.Economy.Components.Demo/CanDoItAll.Economy.Components.Demo.csproj" \
    /m:1 \
    /p:CanDoItAllPackageBaseVersion="$CDA_COMPONENTS_PACKAGE_VERSION" \
    /p:CanDoItAllComponentsPackageVersion="$CDA_COMPONENTS_PACKAGE_VERSION" \
    /p:Version="$CDA_COMPONENTS_PACKAGE_VERSION" \
    /p:PackageVersion="$CDA_COMPONENTS_PACKAGE_VERSION"

FROM build AS publish
RUN dotnet publish "examples/CanDoItAll.Economy.Components.Demo/CanDoItAll.Economy.Components.Demo.csproj" \
    --configuration Release \
    --no-restore \
    /m:1 \
    --output /app/publish \
    /p:UseAppHost=false \
    /p:CanDoItAllPackageBaseVersion="$CDA_COMPONENTS_PACKAGE_VERSION" \
    /p:CanDoItAllComponentsPackageVersion="$CDA_COMPONENTS_PACKAGE_VERSION" \
    /p:Version="$CDA_COMPONENTS_PACKAGE_VERSION" \
    /p:PackageVersion="$CDA_COMPONENTS_PACKAGE_VERSION"

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_EnableDiagnostics=0

EXPOSE 8080
EXPOSE 8443

USER root
RUN mkdir -p /var/lib/candoitall-economy-demo/keys \
    && chown -R "$APP_UID:$APP_UID" /var/lib/candoitall-economy-demo

COPY --from=publish /app/publish .

USER $APP_UID
ENTRYPOINT ["dotnet", "CanDoItAll.Economy.Components.Demo.dll"]
