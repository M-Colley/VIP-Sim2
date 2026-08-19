#!/bin/bash
# Run the capture plugin against a real desktop portal, unattended.
#
# Everything a desktop normally provides has to be started by hand: a session bus,
# PipeWire, WirePlumber, xdg-desktop-portal and its wlroots backend, all inside a nested
# Sway session. xdg-desktop-portal-wlr would normally ask which output to share; with a
# single output and chooser_type=none it picks without a dialog, which is what makes this
# runnable in CI and on a build machine.
#
# It also paints the background between two colours while the test runs. wlroots only
# sends a frame when the output is damaged, so against a static desktop a perfectly
# healthy capture delivers exactly one frame -- which reads like a broken one.
#
# Requires: sway, dbus-run-session, pipewire, wireplumber, xdg-desktop-portal,
#           xdg-desktop-portal-wlr. Build first with ./build.sh.
set -u
HERE=$(cd "$(dirname "$0")" && pwd)
P="$HERE/build"
OUT=${TMPDIR:-/tmp}/vipsim-capture-live
RT=${TMPDIR:-/tmp}/vipsim-capture-runtime
SECONDS_TO_RUN=${1:-12}

[ -x "$P/testcapture" ] || { echo "build/testcapture missing -- run ./build.sh first"; exit 1; }
rm -rf "$OUT"; mkdir -p "$OUT"

# Nested Sway needs to reach the compositor we are already running under. An absolute
# path is used verbatim by libwayland, which matters because XDG_RUNTIME_DIR is about to
# be repointed.
HOSTSOCK="${XDG_RUNTIME_DIR:-/run/user/$(id -u)}/${WAYLAND_DISPLAY:-wayland-0}"
[ -S "$HOSTSOCK" ] || { echo "no host wayland socket at $HOSTSOCK"; exit 1; }
echo "host compositor: $HOSTSOCK"

PORTAL=$(ls /usr/libexec/xdg-desktop-portal /usr/lib/xdg-desktop-portal \
            /usr/lib/x86_64-linux-gnu/xdg-desktop-portal 2>/dev/null | head -1)
WLR=$(ls /usr/libexec/xdg-desktop-portal-wlr /usr/lib/xdg-desktop-portal-wlr \
         /usr/lib/x86_64-linux-gnu/xdg-desktop-portal-wlr 2>/dev/null | head -1)
echo "portal:      ${PORTAL:-NOT FOUND}"
echo "wlr backend: ${WLR:-NOT FOUND}"
[ -n "$PORTAL" ] && [ -n "$WLR" ] || { echo "install xdg-desktop-portal and xdg-desktop-portal-wlr"; exit 1; }

mkdir -p ~/.config/xdg-desktop-portal-wlr
cat > ~/.config/xdg-desktop-portal-wlr/config <<CFG
[screencast]
chooser_type=none
max_fps=30
CFG

INNER="$OUT/inner.sh"
cat > "$INNER" <<INNEREOF
#!/bin/bash
export XDG_CURRENT_DESKTOP=sway
pipewire > $OUT/pipewire.log 2>&1 &
sleep 2
if [ -S "\$XDG_RUNTIME_DIR/pipewire-0" ]; then
    echo "pipewire daemon: up"
else
    echo "pipewire daemon: FAILED TO START -- see $OUT/pipewire.log"
fi
wireplumber > $OUT/wireplumber.log 2>&1 &
sleep 2
$WLR -l DEBUG > $OUT/wlr.log 2>&1 &
sleep 1
$PORTAL -v > $OUT/portal.log 2>&1 &
sleep 3
( while :; do
      swaymsg output '*' bg '#A03020' solid_color >/dev/null 2>&1; sleep 1
      swaymsg output '*' bg '#203050' solid_color >/dev/null 2>&1; sleep 1
  done ) &
PAINTER=\$!
$P/testcapture $SECONDS_TO_RUN > $OUT/testcapture.log 2>&1
echo "testcapture exit=\$?" >> $OUT/testcapture.log
kill \$PAINTER 2>/dev/null
swaymsg exit
INNEREOF
chmod +x "$INNER"

{
  echo 'output * bg #203050 solid_color'
  echo "exec $INNER"
} > "$OUT/sway.conf"

# An isolated runtime dir, so our PipeWire owns its socket.
#
# Without this the daemon collides with one already running on the default runtime dir --
# a desktop session's, or WSLg's -- fails to take pipewire-0.lock, and exits. Every client
# then falls through to the existing daemon, which may have no session manager handling
# video, so the screencast node is published and never linked: the stream reaches "paused"
# and stays there, with nothing in any log to say why.
rm -rf "$RT"; mkdir -p "$RT"; chmod 700 "$RT"
export XDG_RUNTIME_DIR="$RT"
export WAYLAND_DISPLAY="$HOSTSOCK"
export WLR_BACKENDS=wayland
export WLR_RENDERER=pixman
export XDG_CURRENT_DESKTOP=sway

timeout $((SECONDS_TO_RUN + 60)) dbus-run-session -- sway -c "$OUT/sway.conf" > "$OUT/sway.log" 2>&1

echo
grep -a "pipewire daemon" "$OUT/sway.log" 2>/dev/null
echo "=== testcapture ==="
cat "$OUT/testcapture.log" 2>/dev/null || echo "(no output)"
echo
echo "Logs and the captured frame are in $OUT"
