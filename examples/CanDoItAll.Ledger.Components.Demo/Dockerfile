# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0.201 AS build
WORKDIR /src/CanDoItAll.Ledger

COPY . .
RUN dotnet restore "examples/CanDoItAll.Ledger.Components.Demo/CanDoItAll.Ledger.Components.Demo.csproj" \
    /m:1

FROM build AS publish
RUN dotnet publish "examples/CanDoItAll.Ledger.Components.Demo/CanDoItAll.Ledger.Components.Demo.csproj" \
    --configuration Release \
    --no-restore \
    /m:1 \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_EnableDiagnostics=0

EXPOSE 8080
EXPOSE 8443

USER root
RUN mkdir -p /var/lib/candoitall-ledger-demo/keys \
    && chown -R "$APP_UID:$APP_UID" /var/lib/candoitall-ledger-demo

COPY --from=publish /app/publish .

USER $APP_UID
ENTRYPOINT ["dotnet", "CanDoItAll.Ledger.Components.Demo.dll"]
