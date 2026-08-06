#!/usr/bin/env bash
# Runs the host from source against the compose database. Faster than rebuilding
# the image for every iteration; the image is what ships.
set -euo pipefail
[ -f .env ] && { set -a; . ./.env; set +a; }
exec docker run --rm --network host --name s4herp-host-dev \
  -v "$(pwd)":/src -w /src \
  -v s4herp-nuget:/root/.nuget/packages \
  ${CCR_CA_BUNDLE:+-v "$CCR_CA_BUNDLE":/usr/local/share/ca-certificates/ccr.crt:ro} \
  -e HTTP_PROXY="${HTTPS_PROXY:-}" -e HTTPS_PROXY="${HTTPS_PROXY:-}" -e NO_PROXY="${NO_PROXY:-}" \
  -e DOTNET_CLI_TELEMETRY_OPTOUT=1 -e DOTNET_NOLOGO=1 \
  -e ASPNETCORE_ENVIRONMENT=Development \
  -e ASPNETCORE_HTTP_PORTS=8080 \
  -e ConnectionStrings__Default="Server=localhost,1433;Database=S4HERP;User Id=sa;Password=${MSSQL_SA_PASSWORD};Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True" \
  --entrypoint /bin/bash mcr.microsoft.com/dotnet/sdk:10.0 -lc \
  "update-ca-certificates >/dev/null 2>&1 || true; dotnet run --project src/Host/S4HERP.Host --no-launch-profile"
