#!/usr/bin/env bash
# Stage 6 · runs the game in the iOS Simulator: builds the Simulator Xcode project (UC_IOS_SIM=1 tools/unity.sh build-ios),
# installs it on a simulator (default iPhone 18 Pro, with a Dynamic Island), launches it, takes screenshots and notes
# the app's memory. No signing: simulator builds need none.
#   tools/ios/run-sim.sh                 IOS_DEVICE="iPhone 17e" SHOTS="15 40 70" tools/ios/run-sim.sh
set -euo pipefail
cd "$(dirname "$0")/../.."
PROJ=game/Builds/iOSSim/Unity-iPhone.xcodeproj
DEVICE="${IOS_DEVICE:-iPhone 18 Pro Max}"   # a Dynamic Island phone; a name or a UDID
DERIVED=game/Builds/iOSSimDerived
OUT=game/TestResults/ios
mkdir -p "$OUT"
[ -d "$PROJ" ] || { echo "no simulator project: run UC_IOS_SIM=1 tools/unity.sh build-ios"; exit 1; }

rm -rf "$DERIVED/Build/Products"   # never install a previous run's app by mistake
dest="platform=iOS Simulator,name=$DEVICE"; [[ "$DEVICE" =~ ^[0-9A-F-]{36}$ ]] && dest="platform=iOS Simulator,id=$DEVICE"
if ! xcodebuild -project "$PROJ" -scheme Unity-iPhone -configuration Release -sdk iphonesimulator \
  -destination "$dest" -derivedDataPath "$DERIVED" CODE_SIGNING_ALLOWED=NO build -quiet > "$OUT/xcodebuild.log" 2>&1; then
  grep -E "error:" "$OUT/xcodebuild.log" | head -20; echo "xcodebuild failed (log: $OUT/xcodebuild.log)"; exit 1
fi
APP="$(find "$DERIVED/Build/Products" -maxdepth 2 -name '*.app' | head -1)"
[ -n "$APP" ] || { echo "xcodebuild produced no .app"; exit 1; }

xcrun simctl boot "$DEVICE" 2>/dev/null || true
xcrun simctl bootstatus "$DEVICE" -b >/dev/null
xcrun simctl terminate "$DEVICE" ceo.unpaid.game 2>/dev/null || true
xcrun simctl install "$DEVICE" "$APP"
xcrun simctl launch "$DEVICE" ceo.unpaid.game
slug="$(echo "$DEVICE" | tr ' ' '_' | tr -cd '[:alnum:]_')"
t0=0
for t in ${SHOTS:-20 45}; do
  (( t > t0 )) && sleep $((t - t0)); t0=$t
  xcrun simctl io "$DEVICE" screenshot "$OUT/${slug}_${t}s.png" >/dev/null
  pid="$(pgrep -n -f 'UnpaidCEO.app/UnpaidCEO' || true)"
  mem="n/a"; [ -n "$pid" ] && mem="$(footprint -p "$pid" 2>/dev/null | grep -m1 -i 'phys_footprint\|Footprint' || echo n/a)"
  echo "${t}s: $OUT/${slug}_${t}s.png  memory: $mem"
done
