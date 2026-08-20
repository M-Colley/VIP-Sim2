#!/bin/bash
# Run VIP-Sim the way it ships on Linux, and check the things that decide whether it works.
#
# The presenter hosts the player: it serves a Wayland socket of its own, starts the player
# against it, composites the player's buffer onto its layer surface, and hands the user's
# pointer and keyboard back the other way. The player's window therefore never appears on
# the user's desktop -- and this script checks exactly that, by asking the compositor.
#
# Requires: sway, grim, and a build of both halves (./build.sh, and a Linux player).
# Usage: run-hosted-test.sh <player dir> [seconds]
set -u
HERE=$(cd "$(dirname "$0")" && pwd)
PLAYER_DIR=${1:-}
RUN_FOR=${2:-25}
OUT=${TMPDIR:-/tmp}/vipsim-hosted
RT=${TMPDIR:-/tmp}/vipsim-hosted-rt

[ -n "$PLAYER_DIR" ] && [ -d "$PLAYER_DIR" ] || { echo "usage: $0 <player dir> [seconds]"; exit 1; }
[ -x "$HERE/build/vipsim-presenter" ] || { echo "run ./build.sh first"; exit 1; }
rm -rf "$OUT"; mkdir -p "$OUT"

# Stage the pieces the way a release tarball would.
mkdir -p "$PLAYER_DIR/VIP-Sim_Data/Plugins/x86_64"
cp "$HERE/build/libvipsim_present.so" "$HERE/build/libvipsim_capture.so" \
   "$PLAYER_DIR/VIP-Sim_Data/Plugins/x86_64/" 2>/dev/null
cp "$HERE/build/vipsim-presenter" "$PLAYER_DIR/"
chmod +x "$PLAYER_DIR/vipsim-presenter" "$PLAYER_DIR/VIP-Sim" "$PLAYER_DIR/VIP-Sim.sh" 2>/dev/null

# The first-run walkthrough is modal and waits for a click nobody is here to give.
PREFS=~/.config/unity3d/Zefwih/VIP-Sim
mkdir -p "$PREFS"
printf '<unity_prefs version_major="1" version_minor="1">\n  <pref name="vipsim.tutorial.done" type="int">1</pref>\n</unity_prefs>\n' > "$PREFS/prefs"

HOSTSOCK="${XDG_RUNTIME_DIR:-/run/user/$(id -u)}/${WAYLAND_DISPLAY:-wayland-0}"
[ -S "$HOSTSOCK" ] || { echo "no host wayland socket at $HOSTSOCK"; exit 1; }

INNER="$OUT/inner.sh"
cat > "$INNER" <<INNEREOF
#!/bin/bash
cd "$PLAYER_DIR"
sh ./VIP-Sim.sh -logFile $OUT/player.log > $OUT/presenter.log 2>&1 &
sleep 14

# A real click, travelling the real path: sway -> the layer surface -> the presenter ->
# the host -> the player. (1196,12) is the toolbar's info button.
swaymsg seat - cursor set 1196 12 > /dev/null 2>&1
sleep 2
swaymsg seat - cursor press button1 > /dev/null 2>&1
sleep 1
swaymsg seat - cursor release button1 > /dev/null 2>&1
sleep 5

grim $OUT/screen.png 2>/dev/null
swaymsg -t get_tree > $OUT/tree.json 2>/dev/null
sleep 1
swaymsg exit
INNEREOF
chmod +x "$INNER"

{ echo 'output * bg #2060C0 solid_color'; echo "exec $INNER"; } > "$OUT/sway.conf"

rm -rf "$RT"; mkdir -p "$RT"; chmod 700 "$RT"
export XDG_RUNTIME_DIR="$RT"
export WAYLAND_DISPLAY="$HOSTSOCK"
export WLR_BACKENDS=wayland
export WLR_RENDERER=pixman
export XDG_CURRENT_DESKTOP=sway

timeout $((RUN_FOR + 90)) dbus-run-session -- sway -c "$OUT/sway.conf" > "$OUT/sway.log" 2>&1

echo "=== the presenter and its compositor ==="
grep -aE "^\[(host|presenter)\]" "$OUT/presenter.log" 2>/dev/null | head -16
echo
echo "=== is any VIP-Sim window on the user's desktop? ==="
python3 - "$OUT/tree.json" <<'PYEOF'
import json, sys
try:
    t = json.load(open(sys.argv[1]))
except Exception as e:
    print("  (no tree: %s)" % e); raise SystemExit
found = []
def walk(n):
    if n.get("app_id") == "VIP-Sim" or (n.get("window_properties") or {}).get("class") == "VIP-Sim":
        found.append(n.get("rect"))
    for c in n.get("nodes", []) + n.get("floating_nodes", []):
        walk(c)
walk(t)
print("  " + (str(found) if found else "NONE -- the player lives in the presenter's compositor"))
PYEOF
echo
echo "=== where does the player think the mouse is? ==="
# Unity writes to its own location when -logFile does not take; look in both.
PLOG="$OUT/player.log"
[ -s "$PLOG" ] || PLOG=~/.config/unity3d/Zefwih/VIP-Sim/Player.log
grep -aoE "mouse \([0-9]+,[0-9]+\)" "$PLOG" 2>/dev/null | tail -2
grep -a "running inside the presenter" "$PLOG" 2>/dev/null | head -1
echo
echo "Screenshot and logs in $OUT"
