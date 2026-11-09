#!/usr/bin/env bash
# Deploys the UGS configuration (server/ugs) and the Cloud Code module "UnpaidCeo" to one environment.
# Usage: tools/server/deploy.sh [environment]     (default: production)
#
# The owner signs the CLI in once, in Terminal (keys never pass through chat or this repo):
#   npm install -g ugs && ugs login && ugs config set project-id <Project ID>
# Every deploy re-checks the config against the game and re-runs the server tests first.
set -euo pipefail
cd "$(dirname "$0")/../.."
ENV="${1:-production}"
DOTNET="${DOTNET:-$HOME/.dotnet/dotnet}"
[ -x "$DOTNET" ] || DOTNET=dotnet

command -v ugs >/dev/null 2>&1 || { echo "ugs CLI not found: npm install -g ugs"; exit 1; }
status="$(ugs status 2>&1)" || { echo "ugs is not signed in: run 'ugs login' in Terminal"; exit 1; }
if printf '%s' "$status" | grep -qiE "no service account|not logged|not authenticated|missing"; then
  echo "ugs is not signed in: run 'ugs login' in Terminal"; exit 1
fi
project="$(ugs config get project-id 2>/dev/null | tr -d '[:space:]' || true)"
[[ "$project" =~ ^[0-9a-fA-F-]{36}$ ]] || { echo "no UGS project set: ugs config set project-id <Project ID>"; exit 1; }

python3 tools/server/check_ugs_config.py
tools/server/test.sh --nologo -v quiet

stage="$(mktemp -d)"
trap 'rm -rf "$stage"' EXIT
cp server/ugs/*.lb server/ugs/*.rc server/ugs/*.ac "$stage/"
python3 tools/server/check_ugs_config.py --stamp "$stage"   # weekly boards start at the next Monday reset

"$DOTNET" publish server/cloudcode/UnpaidCeo.CloudCode/UnpaidCeo.CloudCode.csproj -c Release -o "$stage/publish" --nologo -v quiet
(cd "$stage/publish" && zip -qr "$stage/UnpaidCeo.ccm" .)   # the module is named after the archive: UnpaidCeo
rm -rf "$stage/publish"

echo "deploying to project $project, environment $ENV:"
ls -1 "$stage"
ugs deploy "$stage" -e "$ENV"
