#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
cd "$SCRIPT_DIR"

osascript <<EOF
set selectedEdition to choose from list {"HTML", "+E+RIS"} with title "Launch Tetris" with prompt "Version" default items {"HTML"} OK button name "Run" cancel button name "Cancel"
if selectedEdition is false then
    return
end if
set editionName to item 1 of selectedEdition
if editionName is "HTML" then
    do shell script "open " & quoted form of POSIX path of "$SCRIPT_DIR/classic-html/index.html"
else if editionName is "+E+RIS" then
    do shell script "open " & quoted form of POSIX path of "$SCRIPT_DIR/enhanced/dist/index.html"
end if
EOF
