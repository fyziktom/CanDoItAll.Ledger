# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0.302 AS build
WORKDIR /src/CanDoItAll.Ledger

COPY . .
RUN mkdir -p /src/CanDoItAll/ExternalPackages
RUN dotnet restore "tools/CanDoItAll.Ledger.DemoSeeder/CanDoItAll.Ledger.DemoSeeder.csproj" \
    /m:1

FROM build AS publish
RUN dotnet publish "tools/CanDoItAll.Ledger.DemoSeeder/CanDoItAll.Ledger.DemoSeeder.csproj" \
    --configuration Release \
    --no-restore \
    /m:1 \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0.10 AS final
WORKDIR /app

ENV DOTNET_EnableDiagnostics=0

USER root
RUN mkdir -p /var/lib/candoitall-ledger-demo/keys \
    && chown -R "$APP_UID:$APP_UID" /var/lib/candoitall-ledger-demo

COPY --from=publish /app/publish .

USER $APP_UID
ENTRYPOINT ["dotnet", "CanDoItAll.Ledger.DemoSeeder.dll"]
