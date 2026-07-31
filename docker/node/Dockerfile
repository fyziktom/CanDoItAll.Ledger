# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0.302 AS build
WORKDIR /src/CanDoItAll.Ledger

COPY . .
RUN dotnet restore "src/CanDoItAll.Ledger.Node/CanDoItAll.Ledger.Node.csproj" \
    /m:1

FROM build AS publish
RUN dotnet publish "src/CanDoItAll.Ledger.Node/CanDoItAll.Ledger.Node.csproj" \
    --configuration Release \
    --no-restore \
    /m:1 \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.10 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_EnableDiagnostics=0

EXPOSE 8080

USER root
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /var/lib/candoitall-ledger/dpkeys \
    && chown -R "$APP_UID:$APP_UID" /var/lib/candoitall-ledger

COPY --from=publish /app/publish .

USER $APP_UID
ENTRYPOINT ["dotnet", "CanDoItAll.Ledger.Node.dll"]
