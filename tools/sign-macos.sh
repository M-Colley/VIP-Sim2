#!/bin/bash
# Sign and notarise VIP-Sim.app for distribution.
#
# Run this ON A MAC, after copying the build across. It cannot be run from the Windows
# build machine: codesign and notarytool are macOS-only, and the signature must be applied
# after the bundle has its executable bit restored, or the signed binary will not launch.
#
# WITHOUT this, macOS Gatekeeper blocks the app outright and the only way in is the
# chmod/xattr workaround in docs/MACOS_README.md. That is acceptable for a developer and
# not acceptable for anyone you sell to.
#
# Prerequisites, none of which can be scripted for you:
#   1. An Apple Developer Program membership (~99 USD/year).
#   2. A "Developer ID Application" certificate in your login keychain.
#        Xcode > Settings > Accounts > Manage Certificates > + > Developer ID Application
#   3. An app-specific password for notarisation, stored in the keychain:
#        xcrun notarytool store-credentials "VIPSIM_NOTARY" \
#          --apple-id "you@example.com" --team-id "TEAMID" --password "app-specific-pw"
#
# Usage:
#   ./sign-macos.sh /path/to/VIP-Sim.app "Developer ID Application: Your Name (TEAMID)"

set -euo pipefail

APP="${1:-}"
IDENTITY="${2:-}"
KEYCHAIN_PROFILE="${3:-VIPSIM_NOTARY}"

if [ -z "$APP" ] || [ -z "$IDENTITY" ]; then
    echo "usage: $0 <path-to-VIP-Sim.app> <signing-identity> [keychain-profile]" >&2
    echo >&2
    echo "List available identities with:  security find-identity -v -p codesigning" >&2
    exit 2
fi

[ -d "$APP" ] || { echo "error: $APP is not a bundle" >&2; exit 1; }

echo "==> Restoring the execute bit"
# Cross-built on Windows, whose filesystem cannot represent it. Signing a bundle whose
# executable is not executable produces a signature over something that cannot run.
chmod +x "$APP/Contents/MacOS/"* 2>/dev/null || true

echo "==> Clearing quarantine"
xattr -cr "$APP" 2>/dev/null || true

echo "==> Signing nested code first"
# Order matters: codesign requires inside-out signing. Unity bundles ship .dylib and
# .bundle plug-ins, and signing the app before its contents invalidates the outer
# signature the moment the loader touches an unsigned inner binary.
find "$APP/Contents" \( -name "*.dylib" -o -name "*.bundle" -o -name "*.so" \) -print0 |
    while IFS= read -r -d '' lib; do
        codesign --force --timestamp --options runtime --sign "$IDENTITY" "$lib"
    done

echo "==> Signing the bundle"
# --options runtime enables the hardened runtime, which notarisation requires.
# The entitlements matter: VIP-Sim uses the camera for gaze tracking and Unity's Mono
# runtime needs the JIT-related exemptions, and without them the signed app crashes on
# launch rather than being rejected at signing time -- a much harder failure to diagnose.
ENTITLEMENTS="$(dirname "$0")/vipsim.entitlements"
if [ -f "$ENTITLEMENTS" ]; then
    codesign --force --deep --timestamp --options runtime \
             --entitlements "$ENTITLEMENTS" --sign "$IDENTITY" "$APP"
else
    echo "    note: no entitlements file found; signing without one." >&2
    codesign --force --deep --timestamp --options runtime --sign "$IDENTITY" "$APP"
fi

echo "==> Verifying"
codesign --verify --deep --strict --verbose=2 "$APP"

echo "==> Notarising (this uploads the app to Apple and can take several minutes)"
ZIP="$(mktemp -d)/VIP-Sim.zip"
ditto -c -k --keepParent "$APP" "$ZIP"
xcrun notarytool submit "$ZIP" --keychain-profile "$KEYCHAIN_PROFILE" --wait

echo "==> Stapling the ticket"
# Staples the notarisation result into the bundle so it launches on machines that are
# offline; without this Gatekeeper has to reach Apple on first run.
xcrun stapler staple "$APP"
xcrun stapler validate "$APP"

echo
echo "Done. $APP is signed, notarised and stapled."
echo "It will now open without the chmod/xattr workaround."
