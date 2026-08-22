#!/bin/bash
# Assemble the release archives, exactly as the download buttons expect to find them.
#
# Run from WSL or any Linux shell with zip installed. Producing the archives here rather
# than on Windows is deliberate: zip preserves the Unix execute bit, so the macOS app can be
# opened without repairing it first. setup.sh still ships, because the quarantine flag has
# to be cleared on the user's machine either way and because an archive re-rolled on Windows
# would lose the bit again.
#
# Asset names carry no version number. The website links to
# releases/latest/download/<name>, which GitHub resolves only for an exact name, so a
# version in the file name silently breaks every download button on every release. The
# version travels in the tag, in the CHANGELOG inside each archive, and in the F1 panel.
set -eu

ROOT=$(cd "$(dirname "$0")/.." && pwd)
WIN_BUILD="$ROOT/windows/Build/StandaloneWindows64"
MAC_BUILD="$ROOT/macos/Build/StandaloneOSX"
OUT=${1:-"$ROOT/../VIP-Sim-release"}

[ -f "$WIN_BUILD/VIP-Sim.exe" ]            || { echo "no Windows build at $WIN_BUILD"; exit 1; }
[ -d "$MAC_BUILD/VIP-Sim.app" ]            || { echo "no macOS build at $MAC_BUILD"; exit 1; }
command -v zip >/dev/null                  || { echo "zip is not installed"; exit 1; }

VERSION=$(grep -E "^  bundleVersion:" "$ROOT/windows/ProjectSettings/ProjectSettings.asset" | awk '{print $2}')
echo "packaging VIP-Sim $VERSION"

rm -rf "$OUT"; mkdir -p "$OUT"
STAGE=$(mktemp -d)
trap 'rm -rf "$STAGE"' EXIT

# ---- Windows: the player at the root of the archive, with the documents beside it.
mkdir -p "$STAGE/win"
cp -r "$WIN_BUILD/." "$STAGE/win/"
rm -rf "$STAGE/win/"*_BurstDebugInformation_DoNotShip
cp "$ROOT/docs/WINDOWS_README.md"   "$STAGE/win/READ-ME-FIRST.md"
cp "$ROOT/docs/ACCESSIBILITY.md" "$ROOT/CHANGELOG.md" "$ROOT/LICENSE" \
   "$ROOT/THIRD-PARTY-NOTICES.md" "$STAGE/win/"
( cd "$STAGE/win" && zip -q -r -y "$OUT/VIP-Sim-Windows-x64.zip" . )

# ---- macOS: the bundle, the documents, and the script that repairs what a copy cannot carry.
mkdir -p "$STAGE/mac"
cp -r "$MAC_BUILD/VIP-Sim.app" "$STAGE/mac/"
cp "$ROOT/docs/MACOS_README.md"     "$STAGE/mac/READ-ME-FIRST.md"
cp "$ROOT/tools/macos-setup.sh"     "$STAGE/mac/setup.sh"
cp "$ROOT/docs/ACCESSIBILITY.md" "$ROOT/CHANGELOG.md" "$ROOT/LICENSE" \
   "$ROOT/THIRD-PARTY-NOTICES.md" "$STAGE/mac/"
chmod +x "$STAGE/mac/VIP-Sim.app/Contents/MacOS/VIP-Sim" "$STAGE/mac/setup.sh"
( cd "$STAGE/mac" && zip -q -r -y "$OUT/VIP-Sim-macOS-universal.zip" . )

# ---- Checksums, so a truncated download can be told from a broken build.
( cd "$OUT" && sha256sum VIP-Sim-Windows-x64.zip VIP-Sim-macOS-universal.zip > SHA256SUMS.txt )

echo
ls -lh "$OUT"
echo
echo "the macOS binary's execute bit, as stored in the archive:"
unzip -Z "$OUT/VIP-Sim-macOS-universal.zip" "VIP-Sim.app/Contents/MacOS/VIP-Sim" | head -2
