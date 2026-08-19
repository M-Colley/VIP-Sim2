#!/bin/bash
# Run the whole Linux path once: player -> capture -> effects -> presenter -> compositor.
#
# Stands up the same nested Sway session and portal stack as run-capture-test.sh, then
# stages the native libraries into the player and starts it. The player is expected to
# launch the presenter itself, so this script starts one process, not two.
#
# Usage: run-endtoend-test.sh /path/to/Build/StandaloneLinux64 [seconds]
set -u
HERE=$(cd "$(dirname "$0")" && pwd)
PLAYER_DIR=${1:-}
RUN_FOR=${2:-25}
OUT=${TMPDIR:-/tmp}/vipsim-e2e
RT=${TMPDIR:-/tmp}/vipsim-e2e-runtime

[ -n "$PLAYER_DIR" ] && [ -d "$PLAYER_DIR" ] || { echo "usage: $0 <player dir> [seconds]"; exit 1; }
PLAYER="$PLAYER_DIR/VIP-Sim"
[ -x "$PLAYER" ] || chmod +x "$PLAYER" 2>/dev/null
[ -f "$PLAYER" ] || { echo "no VIP-Sim binary in $PLAYER_DIR"; exit 1; }
[ -x "$HERE/build/vipsim-presenter" ] || { echo "run ./build.sh first"; exit 1; }

rm -rf "$OUT"; mkdir -p "$OUT"

# Stage the natives where Unity resolves them: the plugin directory, not beside the binary.
PLUGDIR="$PLAYER_DIR/VIP-Sim_Data/Plugins/x86_64"
mkdir -p "$PLUGDIR"
cp "$HERE/build/libvipsim_present.so" "$HERE/build/libvipsim_capture.so" "$PLUGDIR/"
# The presenter is a program, not a plugin, and belongs beside the player.
cp "$HERE/build/vipsim-presenter" "$PLAYER_DIR/"
chmod +x "$PLAYER_DIR/vipsim-presenter"
echo "staged: $(ls "$PLUGDIR" | tr '\n' ' ') and vipsim-presenter"

# Mark the first-run tutorial as seen. It is modal and waits for a click, and there is
# nobody here to click it -- so without this the run only ever tests the walkthrough.
PREFS=~/.config/unity3d/Zefwih/VIP-Sim
mkdir -p "$PREFS"
cat > "$PREFS/prefs" <<PREFSEOF
<unity_prefs version_major="1" version_minor="1">
  <pref name="vipsim.tutorial.done" type="int">1</pref>
</unity_prefs>
PREFSEOF

HOSTSOCK="${XDG_RUNTIME_DIR:-/run/user/$(id -u)}/${WAYLAND_DISPLAY:-wayland-0}"
[ -S "$HOSTSOCK" ] || { echo "no host wayland socket at $HOSTSOCK"; exit 1; }

PORTAL=$(ls /usr/libexec/xdg-desktop-portal /usr/lib/xdg-desktop-portal \
            /usr/lib/x86_64-linux-gnu/xdg-desktop-portal 2>/dev/null | head -1)
WLR=$(ls /usr/libexec/xdg-desktop-portal-wlr /usr/lib/xdg-desktop-portal-wlr \
         /usr/lib/x86_64-linux-gnu/xdg-desktop-portal-wlr 2>/dev/null | head -1)
[ -n "$PORTAL" ] && [ -n "$WLR" ] || { echo "install xdg-desktop-portal and xdg-desktop-portal-wlr"; exit 1; }

mkdir -p ~/.config/xdg-desktop-portal-wlr
printf '[screencast]\nchooser_type=none\nmax_fps=30\n' > ~/.config/xdg-desktop-portal-wlr/config

INNER="$OUT/inner.sh"
cat > "$INNER" <<INNEREOF
#!/bin/bash
export XDG_CURRENT_DESKTOP=sway
pipewire > $OUT/pipewire.log 2>&1 &
sleep 2
wireplumber > $OUT/wireplumber.log 2>&1 &
sleep 2
$WLR -l DEBUG > $OUT/wlr.log 2>&1 &
sleep 1
$PORTAL -v > $OUT/portal.log 2>&1 &
sleep 3

# Something recognisable to capture, so the overlay can be told from a blank screen.
swaymsg output '*' bg '#2060C0' solid_color >/dev/null 2>&1

cd "$PLAYER_DIR"
./VIP-Sim -logFile $OUT/player.log > $OUT/player.stdout 2>&1 &
PLAYERPID=\$!
sleep $RUN_FOR

# What the compositor is actually showing, which is the only thing that settles it.
grim $OUT/screen.png 2>>$OUT/grim.log || echo "grim failed" >> $OUT/grim.log
swaymsg -t get_tree > $OUT/tree.json 2>/dev/null
ps -o pid,comm -u \$(id -u) | grep -E "vipsim|VIP-Sim" > $OUT/procs.txt 2>/dev/null

kill \$PLAYERPID 2>/dev/null
sleep 2
swaymsg exit
INNEREOF
chmod +x "$INNER"

{
  echo 'output * bg #2060C0 solid_color'
  echo "exec $INNER"
} > "$OUT/sway.conf"

rm -rf "$RT"; mkdir -p "$RT"; chmod 700 "$RT"
export XDG_RUNTIME_DIR="$RT"
export WAYLAND_DISPLAY="$HOSTSOCK"
export WLR_BACKENDS=wayland
export WLR_RENDERER=pixman
export XDG_CURRENT_DESKTOP=sway

timeout $((RUN_FOR + 90)) dbus-run-session -- sway -c "$OUT/sway.conf" > "$OUT/sway.log" 2>&1
echo "sway exit=$?"

echo
echo "=== VIP-Sim's Linux seams ==="
grep -aE "LinuxPresenter|LinuxCapture|vipsim_capture|vipsim_present" "$OUT/player.log" \
     "$OUT/player.stdout" 2>/dev/null | sed 's/^[^:]*log://' | head -25
echo
echo "=== processes that were running ==="
cat "$OUT/procs.txt" 2>/dev/null || echo "(none captured)"
echo
echo "Screenshot and logs in $OUT"
