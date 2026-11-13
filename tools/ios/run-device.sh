#!/usr/bin/env bash
# Stage 6 · installs the game on the connected iPhone and starts it: builds the device Xcode project
# (tools/unity.sh build-ios) with the owner's team (automatic signing, Xcode signed in), installs, launches.
#   tools/ios/run-device.sh            IOS_UDID=<udid> tools/ios/run-device.sh
set -euo pipefail
cd "$(dirname "$0")/../.."
PROJ=game/Builds/iOS/Unity-iPhone.xcodeproj
DERIVED=game/Builds/iOSDeviceDerived
TEAM="${IOS_TEAM:-5MT347728K}"
[ -d "$PROJ" ] || { echo "no device project: run tools/unity.sh build-ios"; exit 1; }
UDID="${IOS_UDID:-$(xcrun devicectl list devices 2>/dev/null | awk '/connected/ && /physical/ {for(i=1;i<=NF;i++) if ($i ~ /^[0-9A-F]{8}-[0-9A-F]{16}$/) print $i}' | head -1)}"
[ -n "$UDID" ] || { echo "no iPhone connected (unlock it and trust this Mac)"; exit 1; }
mkdir -p game/TestResults/ios
rm -rf "$DERIVED/Build/Products"
if ! xcodebuild -project "$PROJ" -scheme Unity-iPhone -configuration Release -destination "id=$UDID" -derivedDataPath "$DERIVED" \
  DEVELOPMENT_TEAM="$TEAM" CODE_SIGN_STYLE=Automatic -allowProvisioningUpdates -allowProvisioningDeviceRegistration \
  build -quiet > game/TestResults/ios/device-build.log 2>&1; then
  grep -E "error:" game/TestResults/ios/device-build.log | head -20; exit 1
fi
APP="$(find "$DERIVED/Build/Products" -maxdepth 2 -name '*.app' | head -1)"
xcrun devicectl device install app --device "$UDID" "$APP" >/dev/null
xcrun devicectl device process launch --device "$UDID" ceo.unpaid.game
