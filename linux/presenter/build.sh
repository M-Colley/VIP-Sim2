#!/bin/bash
# Build the VIP-Sim Wayland presenter, the producer library Unity loads, and a test
# producer that stands in for Unity.
#
# wayland-scanner turns the protocol XML into C: a client header of the request/event
# stubs, and a "private-code" translation unit holding the interface tables the client
# links against. Both are generated rather than vendored, so they always match the XML.
set -e
cd "$(dirname "$0")"

XML=protocols/wlr-layer-shell-unstable-v1.xml
[ -f "$XML" ] || { echo "missing $XML"; exit 1; }

for tool in wayland-scanner pkg-config gcc; do
    command -v "$tool" >/dev/null || { echo "missing $tool -- see README.md"; exit 1; }
done
pkg-config --exists wayland-client || {
    echo "missing wayland-client development files -- see README.md"; exit 1; }

# layer-shell's get_popup request takes an xdg_popup, so the generated code references
# xdg_popup_interface and the xdg-shell interface table must be linked in as well.
# Taken from the system wayland-protocols package rather than vendored: it is a stable
# upstream protocol, and a second copy would only drift.
XDG="$(pkg-config --variable=pkgdatadir wayland-protocols)/stable/xdg-shell/xdg-shell.xml"
[ -f "$XDG" ] || { echo "missing xdg-shell.xml -- install wayland-protocols"; exit 1; }

mkdir -p build
wayland-scanner client-header "$XML" build/wlr-layer-shell-unstable-v1-client-protocol.h
wayland-scanner private-code  "$XML" build/wlr-layer-shell-unstable-v1-protocol.c
wayland-scanner client-header "$XDG" build/xdg-shell-client-protocol.h
wayland-scanner private-code  "$XDG" build/xdg-shell-protocol.c

WARN="-Wall -Wextra -Wno-unused-parameter"

# 1. The producer library Unity loads. No Wayland dependency at all.
gcc -O2 $WARN -fPIC -fvisibility=hidden -shared \
    -o build/libvipsim_present.so \
    vipsim_present.c -lrt
echo "built: build/libvipsim_present.so"

# 2. The presenter, which owns the layer surface.
gcc -O2 $WARN -I build -I . \
    -o build/vipsim-presenter \
    presenter.c \
    build/wlr-layer-shell-unstable-v1-protocol.c \
    build/xdg-shell-protocol.c \
    $(pkg-config --cflags --libs wayland-client) -lrt
echo "built: build/vipsim-presenter"

# 3. A stand-in for Unity, linked against the real library.
gcc -O2 $WARN -I . \
    -o build/testproducer \
    testproducer.c -L build -lvipsim_present -Wl,-rpath,'$ORIGIN' -lm
echo "built: build/testproducer"
