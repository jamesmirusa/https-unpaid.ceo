#!/usr/bin/env bash
# Runs one editor method in batchmode:  tools/unity-run.sh <Namespace.Class.Method> [log name]
#   tools/unity-run.sh UnpaidCeo.Editor.StringTableImporter.ImportAll      # UI.tsv / Academy.tsv -> string tables
#   tools/unity-run.sh UnpaidCeo.Editor.DistrictBuilder.BuildAll           # rebuild the district scenes
set -uo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
METHOD="${1:?method}"
LOG="$ROOT/game/Logs/${2:-run}.log"
UNITY="${UNITY_PATH:-}"
if [ -z "$UNITY" ]; then
  ED="$(ls -d /Applications/Unity/Hub/Editor/6000.* 2>/dev/null | sort -V | tail -1)"
  UNITY="$ED/Unity.app/Contents/MacOS/Unity"
fi
[ -x "$UNITY" ] || { echo "Unity not found: $UNITY" >&2; exit 2; }
mkdir -p "$ROOT/game/Logs"
"$UNITY" -batchmode -quit -projectPath "$ROOT/game" -executeMethod "$METHOD" -logFile "$LOG"
code=$?
grep -E "error CS[0-9]+|Exception:|Scripts have compiler errors" "$LOG" | head -20
echo "$METHOD exit=$code"
exit $code
