#!/bin/bash
# VIP-Sim macOS first-run setup.
#
# This build is produced on Windows, whose filesystem cannot store the Unix execute
# permission bit, and the app is not code-signed. Without both fixes below macOS reports
# that the application "cannot be opened" or "is damaged" -- which looks like a broken
# build but is not.
#
# Run with:   bash setup.sh
# (Deliberately invoked through bash: this script would need its own execute bit
# otherwise, which is the very thing that did not survive the copy.)

set -e
cd "$(dirname "$0")"

APP="VIP-Sim.app"

if [ ! -d "$APP" ]; then
    echo "error: $APP is not next to this script."
    echo "Unzip the archive first, then run this from the folder containing $APP."
    exit 1
fi

echo "Restoring the execute bit..."
chmod +x "$APP/Contents/MacOS/VIP-Sim"

echo "Clearing the quarantine flag..."
xattr -dr com.apple.quarantine "$APP" 2>/dev/null || true

echo
echo "Done. Open $APP normally."
echo
echo "On first launch macOS will ask for two permissions, both under"
echo "System Settings > Privacy & Security:"
echo
echo "  Screen Recording  - required, or the window capture is blank."
echo "                      QUIT AND REOPEN VIP-Sim after granting; macOS does"
echo "                      not apply this to an already-running app."
echo "  Camera            - optional, for webcam eye tracking. Decline it and"
echo "                      VIP-Sim follows the mouse instead, which works fully."
echo
echo "Ctrl+Alt+Q quits at any time. VIP-Sim is a borderless always-on-top overlay"
echo "with no title bar, so this is the guaranteed way out."
