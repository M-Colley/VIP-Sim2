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

# ---- Linux: a tarball, and only if both halves are built.
#
# Two binaries, not one. The overlay has to own a layer surface and Unity's window cannot be
# one, so the presenter is a separate program that hosts the player -- and it is the thing
# the launcher starts. A tarball rather than a zip because the execute bit on three files is
# load-bearing and tar keeps it without ceremony.
LIN_BUILD="$ROOT/windows/Build/StandaloneLinux64"
PRESENTER="$ROOT/linux/presenter/build/vipsim-presenter"
if [ -f "$LIN_BUILD/VIP-Sim" ] && [ -f "$PRESENTER" ]; then
    mkdir -p "$STAGE/lin/VIP-Sim"
    cp -r "$LIN_BUILD/." "$STAGE/lin/VIP-Sim/"
    rm -rf "$STAGE/lin/VIP-Sim/"*_BurstDebugInformation_DoNotShip
    cp "$PRESENTER" "$STAGE/lin/VIP-Sim/"
    mkdir -p "$STAGE/lin/VIP-Sim/VIP-Sim_Data/Plugins/x86_64"
    cp "$ROOT/linux/presenter/build/libvipsim_present.so"        "$ROOT/linux/presenter/build/libvipsim_capture.so"        "$STAGE/lin/VIP-Sim/VIP-Sim_Data/Plugins/x86_64/" 2>/dev/null || true
    cp "$ROOT/docs/LINUX_README.md" "$STAGE/lin/VIP-Sim/READ-ME-FIRST.md"
    cp "$ROOT/docs/ACCESSIBILITY.md" "$ROOT/CHANGELOG.md" "$ROOT/LICENSE"        "$ROOT/THIRD-PARTY-NOTICES.md" "$STAGE/lin/VIP-Sim/"
    chmod +x "$STAGE/lin/VIP-Sim/VIP-Sim" "$STAGE/lin/VIP-Sim/vipsim-presenter"              "$STAGE/lin/VIP-Sim/VIP-Sim.sh" 2>/dev/null || true
    ( cd "$STAGE/lin" && tar czf "$OUT/VIP-Sim-Linux-x64.tar.gz" VIP-Sim )
    echo "included a Linux tarball"
else
    echo "no Linux archive: needs both a player and linux/presenter/build.sh output"
fi

# ---- Checksums, so a truncated download can be told from a broken build.
( cd "$OUT" && sha256sum VIP-Sim-*.zip VIP-Sim-*.tar.gz 2>/dev/null > SHA256SUMS.txt )

echo
ls -lh "$OUT"
echo
echo "the macOS binary's execute bit, as stored in the archive:"
unzip -Z "$OUT/VIP-Sim-macOS-universal.zip" "VIP-Sim.app/Contents/MacOS/VIP-Sim" | head -2
