#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
cd "$SCRIPT_DIR"

MONSTACKA_APP=""
if [ -d "$SCRIPT_DIR/enhanced/src-tauri/target/release/bundle/macos" ]; then
  MONSTACKA_APP="$(find "$SCRIPT_DIR/enhanced/src-tauri/target/release/bundle/macos" -maxdepth 1 -name '*.app' -print -quit)"
fi

if [ -n "$MONSTACKA_APP" ]; then
  open "$MONSTACKA_APP"
  exit 0
fi

osascript <<EOF
display dialog "MonStacka! desktop app was not found.\n\nThis launcher no longer falls back to the browser preview, because that can launch a broken development build.\n\nBuild it from enhanced/ with npm install and npm run tauri:build, or download the packaged desktop artifact from GitHub." buttons {"OK"} default button "OK" with title "MonStacka! Not Built"
EOF
