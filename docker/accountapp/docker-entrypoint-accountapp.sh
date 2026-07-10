#!/bin/sh
set -eu

json_escape() {
    printf '%s' "$1" | sed 's/\\/\\\\/g; s/"/\\"/g'
}

: "${ACCOUNT_APP_NODE_BASE_URI:=http://localhost:5081/}"
: "${ACCOUNT_APP_API_BASE_PATH:=api/v1}"
: "${ACCOUNT_APP_LEDGER_ID:=default}"
: "${ACCOUNT_APP_VAULT_ID:=account-app-default}"
: "${ACCOUNT_APP_DEFAULT_ACCOUNT_NAME:=Browser account}"
: "${ACCOUNT_APP_ENABLE_IPFS_UPLOADS:=false}"
: "${ACCOUNT_APP_IPFS_API_BASE_URI:=http://localhost:5081/ipfs-api/}"
: "${ACCOUNT_APP_IPFS_GATEWAY_BASE_URI:=http://localhost:5001/}"

if [ "${ACCOUNT_APP_SCENARIO_ID:-}" = "" ] || [ "${ACCOUNT_APP_SCENARIO_ID:-}" = "__null__" ]; then
    scenario_json="null"
else
    scenario_json="\"$(json_escape "$ACCOUNT_APP_SCENARIO_ID")\""
fi

cat > /usr/share/nginx/html/appsettings.json <<EOF
{
  "AccountApp": {
    "NodeBaseUri": "$(json_escape "$ACCOUNT_APP_NODE_BASE_URI")",
    "ApiBasePath": "$(json_escape "$ACCOUNT_APP_API_BASE_PATH")",
    "LedgerId": "$(json_escape "$ACCOUNT_APP_LEDGER_ID")",
    "ScenarioId": $scenario_json,
    "VaultId": "$(json_escape "$ACCOUNT_APP_VAULT_ID")",
    "DefaultAccountName": "$(json_escape "$ACCOUNT_APP_DEFAULT_ACCOUNT_NAME")",
    "EnableIpfsUploads": $ACCOUNT_APP_ENABLE_IPFS_UPLOADS,
    "IpfsApiBaseUri": "$(json_escape "$ACCOUNT_APP_IPFS_API_BASE_URI")",
    "IpfsGatewayBaseUri": "$(json_escape "$ACCOUNT_APP_IPFS_GATEWAY_BASE_URI")"
  }
}
EOF
