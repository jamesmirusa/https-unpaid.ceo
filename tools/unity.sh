#!/usr/bin/env bash
# Unity batchmode wrapper.
#   tools/unity.sh test-editmode [testFilter]
#   tools/unity.sh test-playmode [testFilter]
#   tools/unity.sh build-web
#   tools/unity.sh build-ios          (UC_IOS_SIM=1 for the simulator)
# Unity path: $UNITY_PATH, else the newest /Applications/Unity/Hub/Editor/6000.*
set -uo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PROJECT="$ROOT/game"
UNITY="${UNITY_PATH:-}"
if [ -z "$UNITY" ]; then
  ED="$(ls -d /Applications/Unity/Hub/Editor/6000.* 2>/dev/null | sort -V | tail -1)"
  UNITY="$ED/Unity.app/Contents/MacOS/Unity"
fi
[ -x "$UNITY" ] || { echo "Unity not found: $UNITY" >&2; exit 2; }
mkdir -p "$PROJECT/TestResults" "$PROJECT/Logs"

summarize() {
  local xml="$1"
  if [ -f "$xml" ]; then
    grep -m1 -o '<test-run[^>]*>' "$xml" | grep -oE '(result|total|passed|failed|skipped)="[^"]*"' | tr '\n' ' '; echo
    grep -oE '<test-case [^>]*result="Failed"[^>]*>' "$xml" | grep -oE 'fullname="[^"]*"' | head -20
  else
    echo "no results file: $xml"
    grep -E "error CS[0-9]+|Aborting batchmode|Scripts have compiler errors" "$PROJECT/Logs/$CMD.log" | head -30
  fi
}

CMD="${1:-}"
case "$CMD" in
  test-editmode|test-playmode)
    if [ "$CMD" = "test-editmode" ]; then PLATFORM=EditMode; OUT=editmode; else PLATFORM=PlayMode; OUT=playmode; fi
    RESULTS="$PROJECT/TestResults/$OUT.xml"
    rm -f "$RESULTS"
    FILTER=()
    if [ -n "${2:-}" ]; then FILTER=(-testFilter "$2"); fi
    # tests run against the web target: art budgets are web caps, and the last build may have left iOS active
    "$UNITY" -batchmode -buildTarget WebGL -projectPath "$PROJECT" -runTests -testPlatform "$PLATFORM" \
      -testResults "$RESULTS" -logFile "$PROJECT/Logs/$CMD.log" ${FILTER[@]+"${FILTER[@]}"}
    code=$?
    summarize "$RESULTS"
    echo "exit=$code"
    exit $code
    ;;
  build-web)
    "$UNITY" -batchmode -quit -buildTarget WebGL -projectPath "$PROJECT" \
      -executeMethod UnpaidCeo.Editor.BuildScript.BuildWeb -logFile "$PROJECT/Logs/build-web.log"
    code=$?
    grep -E "error CS[0-9]+|Build (succeeded|failed)|\[build\]" "$PROJECT/Logs/build-web.log" | tail -20
    echo "exit=$code"
    exit $code
    ;;
  build-ios)
    # UC_IOS_SIM=1 tools/unity.sh build-ios → Builds/iOSSim (Simulator SDK); otherwise Builds/iOS (device, signing in Xcode)
    "$UNITY" -batchmode -quit -buildTarget iOS -projectPath "$PROJECT" \
      -executeMethod UnpaidCeo.Editor.BuildScript.BuildIos -logFile "$PROJECT/Logs/build-ios.log"
    code=$?
    grep -E "error CS[0-9]+|Build (succeeded|failed)|\[build\]" "$PROJECT/Logs/build-ios.log" | tail -20
    echo "exit=$code"
    exit $code
    ;;
  *)
    echo "usage: tools/unity.sh test-editmode|test-playmode [filter] | build-web | build-ios" >&2
    exit 2
    ;;
esac
