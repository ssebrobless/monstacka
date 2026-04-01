#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
cd "$SCRIPT_DIR"

ENHANCED_APP=""
if [ -d "$SCRIPT_DIR/enhanced/src-tauri/target/release/bundle/macos" ]; then
  ENHANCED_APP="$(find "$SCRIPT_DIR/enhanced/src-tauri/target/release/bundle/macos" -maxdepth 1 -name '*.app' -print -quit)"
fi

osascript <<EOF
set selectedEdition to choose from list {"HTML", "MonStacka!"} with title "Launch Tetris" with prompt "Version" default items {"HTML"} OK button name "Run" cancel button name "Cancel"
if selectedEdition is false then
    return
end if
set editionName to item 1 of selectedEdition
if editionName is "HTML" then
    do shell script "open " & quoted form of POSIX path of "$SCRIPT_DIR/classic-html/index.html"
else if editionName is "MonStacka!" then
    if "$ENHANCED_APP" is not "" then
        do shell script "open " & quoted form of POSIX path of "$ENHANCED_APP"
    else
        display dialog "MonStacka! desktop app was not found.\n\nThis launcher no longer falls back to the browser preview, because that can launch a broken development build.\n\nBuild it from enhanced/ with npm install and npm run tauri:build, or download the packaged desktop artifact from GitHub." buttons {"OK"} default button "OK" with title "MonStacka! Not Built"
    end if
end if
EOF
