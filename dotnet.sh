#!/usr/bin/env bash
# Runs the .NET 10 SDK in a container so no host SDK install is needed.
# Usage: ./dotnet.sh build S4HERP.sln -c Release
set -euo pipefail
exec docker run --rm --network host \
  -v "$(pwd)":/src -w /src \
  -v s4herp-nuget:/root/.nuget/packages \
  -v s4herp-dotnet-tools:/root/.dotnet/tools \
  ${CCR_CA_BUNDLE:+-v "$CCR_CA_BUNDLE":/usr/local/share/ca-certificates/ccr.crt:ro} \
  -e HTTP_PROXY="${HTTPS_PROXY:-}" -e HTTPS_PROXY="${HTTPS_PROXY:-}" -e NO_PROXY="${NO_PROXY:-}" \
  -e DOTNET_CLI_TELEMETRY_OPTOUT=1 -e DOTNET_NOLOGO=1 \
  -e PATH="/root/.dotnet/tools:/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin:/usr/share/dotnet" \
  --entrypoint /bin/bash mcr.microsoft.com/dotnet/sdk:10.0 -lc \
  "update-ca-certificates >/dev/null 2>&1 || true; dotnet $*"
