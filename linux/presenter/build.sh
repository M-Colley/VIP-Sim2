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

# linux-dmabuf: the zero-copy path. Only its capability probe is used today -- README.md
# says exactly how far that goes and what is still missing.
DMABUF="$(pkg-config --variable=pkgdatadir wayland-protocols)/stable/linux-dmabuf/linux-dmabuf-v1.xml"
[ -f "$DMABUF" ] || { echo "missing linux-dmabuf-v1.xml -- install wayland-protocols"; exit 1; }

mkdir -p build
wayland-scanner client-header "$XML" build/wlr-layer-shell-unstable-v1-client-protocol.h
wayland-scanner private-code  "$XML" build/wlr-layer-shell-unstable-v1-protocol.c
wayland-scanner client-header "$XDG" build/xdg-shell-client-protocol.h
wayland-scanner private-code  "$XDG" build/xdg-shell-protocol.c
wayland-scanner client-header "$DMABUF" build/linux-dmabuf-v1-client-protocol.h
wayland-scanner private-code  "$DMABUF" build/linux-dmabuf-v1-protocol.c

WARN="-Wall -Wextra -Wno-unused-parameter"

# 1. The producer library Unity loads. No Wayland dependency at all.
gcc -O2 $WARN -fPIC -fvisibility=hidden -shared \
    -o build/libvipsim_present.so \
    vipsim_present.c -lrt
echo "built: build/libvipsim_present.so"

# 2. The presenter, which owns the layer surface.
gcc -O2 $WARN -I build -I . \
    -o build/vipsim-presenter \
    presenter.c host.c \
    build/wlr-layer-shell-unstable-v1-protocol.c \
    build/xdg-shell-protocol.c \
    build/linux-dmabuf-v1-protocol.c \
    $(pkg-config --cflags --libs wayland-client wayland-server) -lrt
echo "built: build/vipsim-presenter"

# 3. Screen capture: xdg-desktop-portal for consent, PipeWire for the frames.
#    Optional -- a machine without the development packages can still build the overlay.
if pkg-config --exists gio-2.0 libpipewire-0.3; then
    gcc -O2 $WARN -fPIC -fvisibility=hidden -shared         -o build/libvipsim_capture.so         vipsim_capture.c         $(pkg-config --cflags --libs gio-2.0 libpipewire-0.3) -lpthread
    echo "built: build/libvipsim_capture.so"
else
    echo "skipped libvipsim_capture.so (needs libglib2.0-dev and libpipewire-0.3-dev)"
fi

# 4. Stand-ins for Unity, linked against the real libraries.
gcc -O2 $WARN -I . \
    -o build/testproducer \
    testproducer.c -L build -lvipsim_present -Wl,-rpath,'$ORIGIN' -lm
echo "built: build/testproducer"

# A stand-in for Unity on the capture side. Built only when the plugin was.
if [ -f build/libvipsim_capture.so ]; then
    gcc -O2 $WARN -I . -o build/testcapture         testcapture.c -L build -lvipsim_capture -Wl,-rpath,'$ORIGIN'
    echo "built: build/testcapture"
fi
