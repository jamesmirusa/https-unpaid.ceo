#!/usr/bin/env bash
# Web build size budget: the files under <build>/Build (the first download) must total <= 20 MiB.
# Streamed Addressables content (StreamingAssets/aa: voice, music, districts) is reported but not counted.
#   tools/check-build-size.sh game/Builds/Web
set -euo pipefail
DIR="${1:-game/Builds/Web}"
LIMIT=20971520
[ -d "$DIR/Build" ] || { echo "no Build folder in $DIR" >&2; exit 2; }
total=$(find "$DIR/Build" -type f -exec stat -f%z {} + | awk '{s+=$1} END {print s+0}')
echo "Build/* = $total bytes ($(echo "scale=2; $total/1048576" | bc) MiB), limit $LIMIT"
if [ -d "$DIR/StreamingAssets" ]; then
  streamed=$(find "$DIR/StreamingAssets" -type f -exec stat -f%z {} + | awk '{s+=$1} END {print s+0}')
  echo "StreamingAssets (on demand) = $streamed bytes ($(echo "scale=2; $streamed/1048576" | bc) MiB)"
fi
if [ "$total" -gt "$LIMIT" ]; then
  echo "FAIL: over budget. Largest files:"
  find "$DIR/Build" -type f -exec stat -f'%z %N' {} + | sort -rn | head -10
  exit 1
fi
echo "OK"
