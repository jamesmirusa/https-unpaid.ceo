#!/usr/bin/env bash
# Publishes game/Builds/Web to GitHub Pages (branch gh-pages), which serves https://unpaid.ceo.
# Usage: tools/unity.sh test-playmode && tools/unity.sh build-web && tools/deploy-web.sh
set -euo pipefail
cd "$(dirname "$0")/.."
[ -f game/Builds/Web/index.html ] || { echo "no build: run tools/unity.sh build-web"; exit 1; }
tools/check-build-size.sh | tail -1
SHA=$(git rev-parse --short HEAD)
git fetch -q origin gh-pages:refs/remotes/origin/gh-pages
D=$(mktemp -d); git worktree add -q "$D" origin/gh-pages; trap 'git worktree remove --force "$D"' EXIT
cd "$D"; git checkout -q -B gh-pages origin/gh-pages; git rm -rq .
cp -R "$OLDPWD/game/Builds/Web/." .; touch .nojekyll; echo unpaid.ceo > CNAME   # the custom domain lives in this file
git add -A; git commit -q -m "Unpaid CEO web build ($SHA)"; git push -q origin gh-pages
echo "published $SHA: https://unpaid.ceo (GitHub Pages needs about a minute)"
