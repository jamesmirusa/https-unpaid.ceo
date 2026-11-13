#!/usr/bin/env bash
# Prepares the server for deploying from the Unity Editor (no `ugs login`): checks the config, runs the server tests,
# and copies the leaderboard and Remote Config files from server/ugs (stamped: weekly boards reset next Monday) into
# game/Assets/UnpaidCeo/Server/Deploy, beside the Cloud Code module reference UnpaidCeo.ccmr.
# Then in Unity: Window → Deployment → select "Deploy" items → Deploy (signed in to the Editor with the owner's account).
set -euo pipefail
cd "$(dirname "$0")/../.."
python3 tools/server/check_ugs_config.py
tools/server/test.sh --nologo -v quiet
OUT=game/Assets/UnpaidCeo/Server/Deploy
cp server/ugs/*.lb server/ugs/*.rc "$OUT/"
python3 tools/server/check_ugs_config.py --stamp "$OUT"
echo "ready: open Unity → Window → Deployment → Deploy All (module UnpaidCeo + $(ls "$OUT"/*.lb | wc -l | tr -d ' ') leaderboards + Remote Config)"
