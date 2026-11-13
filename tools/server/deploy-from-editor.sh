#!/usr/bin/env bash
# Deploys the server like the Deployment window's Deploy button, from the command line: prepares the files
# (sync_editor_deploy.sh: config check, server tests, boards stamped), then runs Unity in batchmode, signed in through
# Unity Hub with the owner's account, and deploys Assets/UnpaidCeo/Server/Deploy. The Unity Editor must be closed.
set -euo pipefail
cd "$(dirname "$0")/../.."
ROOT="$(pwd)"
tools/server/sync_editor_deploy.sh
UNITY="${UNITY_PATH:-$(ls -d /Applications/Unity/Hub/Editor/6000.* | sort -V | tail -1)/Unity.app/Contents/MacOS/Unity}"
"$UNITY" -batchmode -projectPath "$ROOT/game" -executeMethod UnpaidCeo.Editor.ServerDeploy.DeployAll -logFile "$ROOT/game/Logs/server-deploy.log"
code=$?
grep "\[deploy\]" "$ROOT/game/Logs/server-deploy.log" | tail -20
exit $code
