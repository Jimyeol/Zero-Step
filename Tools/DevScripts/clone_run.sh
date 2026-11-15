#!/bin/bash
# Run EditMode tests (or VisualCaptureTests) in an APFS clone of the project, so the open Unity editor is not blocked,
# and put the editor's save data back afterwards (test runs overwrite SaveFile.es3 / PlayerPrefs).
# usage: ZS_CLONE=<clone dir> clone_run.sh <testFilter> <outdir> [capture stages]
#   first time: cp -cR <project> "$ZS_CLONE"     env: UNITY_EXTRA=-nographics (needed for GameplayRegressionTests), SRC_ASSETS
set -u
SRC="$(cd "$(dirname "$0")/../.." && pwd)"
DST="${ZS_CLONE:?set ZS_CLONE to the clone directory}"
[ -d "$DST/Assets" ] || { echo "clone missing: cp -cR $SRC $DST"; exit 1; }
rsync -a --delete "${SRC_ASSETS:-$SRC/Assets}/" "$DST/Assets/"; rsync -a --delete "$SRC/Packages/" "$DST/Packages/"; rsync -a --delete "$SRC/ProjectSettings/" "$DST/ProjectSettings/"
rm -f "$DST/.mcp.json"
SAVE_DIR="$HOME/Library/Application Support/DefaultCompany/ZeroStep"
TMPB=$(mktemp -d)
cp "$SAVE_DIR/SaveFile.es3" "$TMPB/SaveFile.es3" 2>/dev/null; ls "$SAVE_DIR" > "$TMPB/files_before.txt" 2>/dev/null
defaults export unity.DefaultCompany.ZeroStep "$TMPB/prefs.plist" 2>/dev/null
mkdir -p "$2"
ZEROSTEP_CAPTURE_DIR="$2" ZEROSTEP_CAPTURE_STAGES="${3:-1}" /Applications/Unity/Hub/Editor/6000.3.5f2/Unity.app/Contents/MacOS/Unity -batchmode ${UNITY_EXTRA:-} -projectPath "$DST" \
  -runTests -testPlatform EditMode -testFilter "$1" -testResults "$2/results.xml" -logFile "$2/unity.log" > /dev/null 2>&1
CODE=$?
if [ -f "$TMPB/SaveFile.es3" ]; then cp "$TMPB/SaveFile.es3" "$SAVE_DIR/SaveFile.es3"; fi
if [ -f "$TMPB/prefs.plist" ]; then defaults delete unity.DefaultCompany.ZeroStep 2>/dev/null; defaults import unity.DefaultCompany.ZeroStep "$TMPB/prefs.plist"; fi
for f in $(ls "$SAVE_DIR" 2>/dev/null); do grep -qx "$f" "$TMPB/files_before.txt" || rm -rf "${SAVE_DIR:?}/${f:?}"; done
rm -rf "${TMPB:?}"
echo "unity_exit=$CODE"; grep -o 'result="[A-Za-z]*" total="[0-9]*" passed="[0-9]*" failed="[0-9]*"' "$2/results.xml" 2>/dev/null | head -1
grep -E "error CS|\[Regression\]|\[StageReplay\] done" "$2/unity.log" | tail -8; grep -A6 "<failure" "$2/results.xml" 2>/dev/null | head -12
