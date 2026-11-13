#!/usr/bin/env bash
# Builds and tests the Cloud Code module (server/cloudcode) with the user-level .NET 8 SDK (~/.dotnet).
# Usage: tools/server/test.sh [extra dotnet test args, e.g. --filter Duel]
set -euo pipefail
cd "$(dirname "$0")/../.."
DOTNET="${DOTNET:-$HOME/.dotnet/dotnet}"
[ -x "$DOTNET" ] || DOTNET=dotnet
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
"$DOTNET" test server/cloudcode/UnpaidCeo.CloudCode.sln "$@"
