// Screen capture for VIP-Sim on Wayland: xdg-desktop-portal + PipeWire.
//
// Windows enumerates other applications' windows and grabs their pixels directly.
// Wayland does not permit that, by design -- a client cannot see, list or read another
// client's surface. That is the security model working, not a gap to route around, and it
// is the same shape as macOS's Screen Recording permission: the user is asked, by the
// system, and hands over one specific source.
//
// So VIP-Sim's own window list does not exist on this platform. The portal shows the
// compositor's own picker, the user chooses a window or an output, and what comes back is
// a PipeWire node carrying frames. This works on GNOME too, where the overlay half of the
// port cannot run at all -- which is why capture is worth having even before that is
// resolved.
//
// The ABI mirrors the MacCapture / uWindowCapture seam so the C# side stays uniform.
//
// Built as libvipsim_capture.so. See docs/LINUX_PORT.md.

#define _GNU_SOURCE
#include <gio/gio.h>
#include <gio/gunixfdlist.h>
#include <pipewire/pipewire.h>
#include <spa/param/video/format-utils.h>
#include <spa/debug/types.h>

#include <pthread.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#define EXPORT __attribute__((visibility("default")))

// ---------------------------------------------------------------- state

enum vipsim_state {
    VIPSIM_IDLE = 0,
    VIPSIM_WAITING_FOR_USER = 1,   // the portal dialog is up
    VIPSIM_STREAMING = 2,
    VIPSIM_FAILED = 3,
    VIPSIM_NO_PORTAL = 4,
};

static struct {
    enum vipsim_state state;
    char              message[256];

    GDBusConnection *bus;
    GMainLoop       *loop;
    GThread         *bus_thread;
    char            *session_handle;
    char            *sender_name;      // unique bus name, dots replaced, for handle tokens
    uint32_t         token_counter;

    struct pw_thread_loop *pw_loop;
    struct pw_context     *pw_context;
    struct pw_core        *pw_core;
    struct pw_stream      *pw_stream;

    pthread_mutex_t  lock;
    uint8_t         *frame;            // BGRA, width*height*4
    uint32_t         width, height;
    uint32_t         seq;
} C;

static void set_state(enum vipsim_state s, const char *fmt, ...)
{
    C.state = s;
    va_list ap;
    va_start(ap, fmt);
    vsnprintf(C.message, sizeof C.message, fmt, ap);
    va_end(ap);
    fprintf(stderr, "[vipsim_capture] %s\n", C.message);
}

// ---------------------------------------------------------------- PipeWire

static void on_stream_param_changed(void *data, uint32_t id, const struct spa_pod *param)
{
    (void)data;
    if (!param || id != SPA_PARAM_Format) return;

    struct spa_video_info info;
    memset(&info, 0, sizeof info);
    if (spa_format_parse(param, &info.media_type, &info.media_subtype) < 0) return;
    if (info.media_type != SPA_MEDIA_TYPE_video ||
        info.media_subtype != SPA_MEDIA_SUBTYPE_raw) return;
    if (spa_format_video_raw_parse(param, &info.info.raw) < 0) return;

    pthread_mutex_lock(&C.lock);
    C.width = info.info.raw.size.width;
    C.height = info.info.raw.size.height;
    free(C.frame);
    C.frame = calloc((size_t)C.width * C.height, 4);
    pthread_mutex_unlock(&C.lock);

    set_state(VIPSIM_STREAMING, "streaming %ux%u", C.width, C.height);
}

static void on_stream_process(void *data)
{
    (void)data;
    struct pw_buffer *b = pw_stream_dequeue_buffer(C.pw_stream);
    if (!b) return;

    struct spa_buffer *buf = b->buffer;
    if (buf->datas[0].data && C.frame) {
        pthread_mutex_lock(&C.lock);
        uint32_t stride = buf->datas[0].chunk->stride;
        uint32_t rows = C.height;
        uint32_t want = C.width * 4;
        const uint8_t *src = buf->datas[0].data;
        if (stride == want) {
            memcpy(C.frame, src, (size_t)want * rows);
        } else {
            for (uint32_t y = 0; y < rows; y++)
                memcpy(C.frame + (size_t)y * want, src + (size_t)y * stride, want);
        }
        C.seq++;
        pthread_mutex_unlock(&C.lock);
    }
    pw_stream_queue_buffer(C.pw_stream, b);
}

static void on_stream_state_changed(void *data, enum pw_stream_state old,
                                    enum pw_stream_state state, const char *error)
{
    (void)data; (void)old;
    if (state == PW_STREAM_STATE_ERROR)
        set_state(VIPSIM_FAILED, "PipeWire stream error: %s", error ? error : "unknown");
}

static const struct pw_stream_events stream_events = {
    PW_VERSION_STREAM_EVENTS,
    .state_changed = on_stream_state_changed,
    .param_changed = on_stream_param_changed,
    .process       = on_stream_process,
};

/// Connect to the node the portal handed us, over the fd it also handed us.
static bool start_pipewire(int fd, uint32_t node_id)
{
    C.pw_loop = pw_thread_loop_new("vipsim-capture", NULL);
    if (!C.pw_loop) { set_state(VIPSIM_FAILED, "could not create the PipeWire loop"); return false; }

    pw_thread_loop_lock(C.pw_loop);
    C.pw_context = pw_context_new(pw_thread_loop_get_loop(C.pw_loop), NULL, 0);
    C.pw_core = pw_context_connect_fd(C.pw_context, fd, NULL, 0);
    if (!C.pw_core) {
        pw_thread_loop_unlock(C.pw_loop);
        set_state(VIPSIM_FAILED, "could not connect to PipeWire");
        return false;
    }

    C.pw_stream = pw_stream_new(C.pw_core, "vipsim-capture",
        pw_properties_new(PW_KEY_MEDIA_TYPE, "Video",
                          PW_KEY_MEDIA_CATEGORY, "Capture",
                          PW_KEY_MEDIA_ROLE, "Screen", NULL));

    static struct spa_hook hook;
    pw_stream_add_listener(C.pw_stream, &hook, &stream_events, NULL);

    uint8_t pod_buf[1024];
    struct spa_pod_builder b = SPA_POD_BUILDER_INIT(pod_buf, sizeof pod_buf);
    struct spa_rectangle def = SPA_RECTANGLE(1920, 1080),
                         min = SPA_RECTANGLE(1, 1),
                         max = SPA_RECTANGLE(8192, 8192);
    struct spa_fraction rdef = SPA_FRACTION(60, 1),
                        rmin = SPA_FRACTION(0, 1),
                        rmax = SPA_FRACTION(240, 1);

    // BGRA matches what the presenter and Unity both want, so ask for it first.
    const struct spa_pod *params[1];
    params[0] = spa_pod_builder_add_object(&b,
        SPA_TYPE_OBJECT_Format, SPA_PARAM_EnumFormat,
        SPA_FORMAT_mediaType,       SPA_POD_Id(SPA_MEDIA_TYPE_video),
        SPA_FORMAT_mediaSubtype,    SPA_POD_Id(SPA_MEDIA_SUBTYPE_raw),
        SPA_FORMAT_VIDEO_format,    SPA_POD_CHOICE_ENUM_Id(3,
                                        SPA_VIDEO_FORMAT_BGRA,
                                        SPA_VIDEO_FORMAT_RGBA,
                                        SPA_VIDEO_FORMAT_BGRx),
        SPA_FORMAT_VIDEO_size,      SPA_POD_CHOICE_RANGE_Rectangle(&def, &min, &max),
        SPA_FORMAT_VIDEO_framerate, SPA_POD_CHOICE_RANGE_Fraction(&rdef, &rmin, &rmax));

    int rc = pw_stream_connect(C.pw_stream, PW_DIRECTION_INPUT, node_id,
                               PW_STREAM_FLAG_AUTOCONNECT | PW_STREAM_FLAG_MAP_BUFFERS,
                               params, 1);
    pw_thread_loop_unlock(C.pw_loop);

    if (rc < 0) { set_state(VIPSIM_FAILED, "could not connect the PipeWire stream"); return false; }
    pw_thread_loop_start(C.pw_loop);
    return true;
}

// ---------------------------------------------------------------- portal

static char *next_token(const char *prefix)
{
    return g_strdup_printf("%s_%u", prefix, ++C.token_counter);
}

/// Call one portal method and wait for its Response signal.
///
/// Every ScreenCast method answers asynchronously on a request object rather than
/// returning a result, so each step is: subscribe to the response path, make the call,
/// spin the loop until it arrives.
static GVariant *portal_call_sync(const char *method, GVariant *args, const char *token,
                                  GUnixFDList **out_fds)
{
    g_autofree char *path = g_strdup_printf("/org/freedesktop/portal/desktop/request/%s/%s",
                                            C.sender_name, token);
    GMainContext *ctx = g_main_context_new();
    g_main_context_push_thread_default(ctx);
    GMainLoop *loop = g_main_loop_new(ctx, FALSE);

    struct { GMainLoop *loop; GVariant *result; } wait = { loop, NULL };

    void on_response(GDBusConnection *conn, const gchar *sender, const gchar *obj,
                     const gchar *iface, const gchar *signal, GVariant *params, gpointer user)
    {
        (void)conn; (void)sender; (void)obj; (void)iface; (void)signal;
        typeof(wait) *w = user;
        w->result = g_variant_ref(params);
        g_main_loop_quit(w->loop);
    }

    guint sub = g_dbus_connection_signal_subscribe(
        C.bus, "org.freedesktop.portal.Desktop", "org.freedesktop.portal.Request",
        "Response", path, NULL, G_DBUS_SIGNAL_FLAGS_NONE, on_response, &wait, NULL);

    GError *err = NULL;
    GVariant *reply = g_dbus_connection_call_with_unix_fd_list_sync(
        C.bus, "org.freedesktop.portal.Desktop", "/org/freedesktop/portal/desktop",
        "org.freedesktop.portal.ScreenCast", method, args, NULL,
        G_DBUS_CALL_FLAGS_NONE, -1, NULL, out_fds, NULL, &err);

    if (err) {
        set_state(VIPSIM_FAILED, "portal %s failed: %s", method, err->message);
        g_error_free(err);
        g_dbus_connection_signal_unsubscribe(C.bus, sub);
        g_main_loop_unref(loop);
        g_main_context_pop_thread_default(ctx);
        g_main_context_unref(ctx);
        return NULL;
    }
    if (reply) g_variant_unref(reply);

    g_main_loop_run(loop);          // returns when Response arrives
    g_dbus_connection_signal_unsubscribe(C.bus, sub);
    g_main_loop_unref(loop);
    g_main_context_pop_thread_default(ctx);
    g_main_context_unref(ctx);
    return wait.result;
}

static bool portal_response_ok(GVariant *response, GVariant **out_results)
{
    if (!response) return false;
    guint32 code = 1;
    GVariant *results = NULL;
    g_variant_get(response, "(u@a{sv})", &code, &results);
    if (code != 0) {
        // 1 = the user cancelled. That is an outcome, not a fault.
        set_state(code == 1 ? VIPSIM_IDLE : VIPSIM_FAILED,
                  code == 1 ? "the user cancelled the capture request"
                            : "the portal refused the request (code %u)", code);
        if (results) g_variant_unref(results);
        return false;
    }
    if (out_results) *out_results = results;
    else if (results) g_variant_unref(results);
    return true;
}

// ---------------------------------------------------------------- public ABI

EXPORT int vipsim_capture_init(void)
{
    if (C.bus) return 0;

    pthread_mutex_init(&C.lock, NULL);
    pw_init(NULL, NULL);

    GError *err = NULL;
    C.bus = g_bus_get_sync(G_BUS_TYPE_SESSION, NULL, &err);
    if (!C.bus) {
        set_state(VIPSIM_NO_PORTAL, "no session bus: %s", err ? err->message : "unknown");
        if (err) g_error_free(err);
        return -1;
    }

    // Handle tokens are built from the unique bus name with dots turned into underscores;
    // the portal derives the request object path the same way, and the two must agree.
    const char *unique = g_dbus_connection_get_unique_name(C.bus);
    C.sender_name = g_strdup(unique && unique[0] == ':' ? unique + 1 : unique);
    for (char *p = C.sender_name; p && *p; p++) if (*p == '.') *p = '_';

    // Is a portal actually there? On a headless box or in WSL it is not, and saying so
    // plainly beats a timeout thirty seconds later.
    GVariant *v = g_dbus_connection_call_sync(
        C.bus, "org.freedesktop.portal.Desktop", "/org/freedesktop/portal/desktop",
        "org.freedesktop.DBus.Properties", "Get",
        g_variant_new("(ss)", "org.freedesktop.portal.ScreenCast", "version"),
        NULL, G_DBUS_CALL_FLAGS_NONE, 2000, NULL, &err);
    if (!v) {
        set_state(VIPSIM_NO_PORTAL,
                  "xdg-desktop-portal with a ScreenCast backend is not available (%s). "
                  "Screen capture needs one; see docs/LINUX_PORT.md.",
                  err ? err->message : "no reply");
        if (err) g_error_free(err);
        return -2;
    }
    g_variant_unref(v);

    set_state(VIPSIM_IDLE, "portal available");
    return 0;
}

EXPORT int vipsim_capture_start(void)
{
    if (!C.bus) return -1;

    // 1. CreateSession
    g_autofree char *st = next_token("vipsim");
    g_autofree char *ss = next_token("vipsimsession");
    GVariantBuilder o;
    g_variant_builder_init(&o, G_VARIANT_TYPE_VARDICT);
    g_variant_builder_add(&o, "{sv}", "handle_token", g_variant_new_string(st));
    g_variant_builder_add(&o, "{sv}", "session_handle_token", g_variant_new_string(ss));

    set_state(VIPSIM_WAITING_FOR_USER, "asking the portal for a source");
    GVariant *resp = portal_call_sync("CreateSession", g_variant_new("(a{sv})", &o), st, NULL);
    GVariant *res = NULL;
    if (!portal_response_ok(resp, &res)) { if (resp) g_variant_unref(resp); return -2; }
    g_variant_lookup(res, "session_handle", "s", &C.session_handle);
    g_variant_unref(res); g_variant_unref(resp);

    // 2. SelectSources -- monitors and windows, one at a time.
    g_autofree char *st2 = next_token("vipsim");
    g_variant_builder_init(&o, G_VARIANT_TYPE_VARDICT);
    g_variant_builder_add(&o, "{sv}", "handle_token", g_variant_new_string(st2));
    g_variant_builder_add(&o, "{sv}", "types", g_variant_new_uint32(1u | 2u));
    g_variant_builder_add(&o, "{sv}", "multiple", g_variant_new_boolean(FALSE));
    g_variant_builder_add(&o, "{sv}", "cursor_mode", g_variant_new_uint32(2u)); // embedded
    resp = portal_call_sync("SelectSources",
                            g_variant_new("(oa{sv})", C.session_handle, &o), st2, NULL);
    if (!portal_response_ok(resp, NULL)) { if (resp) g_variant_unref(resp); return -3; }
    g_variant_unref(resp);

    // 3. Start -- this is where the user sees the picker.
    g_autofree char *st3 = next_token("vipsim");
    g_variant_builder_init(&o, G_VARIANT_TYPE_VARDICT);
    g_variant_builder_add(&o, "{sv}", "handle_token", g_variant_new_string(st3));
    resp = portal_call_sync("Start",
                            g_variant_new("(osa{sv})", C.session_handle, "", &o), st3, NULL);
    if (!portal_response_ok(resp, &res)) { if (resp) g_variant_unref(resp); return -4; }

    uint32_t node_id = 0;
    GVariant *streams = g_variant_lookup_value(res, "streams", G_VARIANT_TYPE("a(ua{sv})"));
    if (streams) {
        GVariantIter it;
        GVariant *props;
        g_variant_iter_init(&it, streams);
        if (g_variant_iter_next(&it, "(u@a{sv})", &node_id, &props)) g_variant_unref(props);
        g_variant_unref(streams);
    }
    g_variant_unref(res); g_variant_unref(resp);

    if (!node_id) { set_state(VIPSIM_FAILED, "the portal returned no stream"); return -5; }

    // 4. A file descriptor for the PipeWire connection.
    GUnixFDList *fds = NULL;
    GError *err = NULL;
    g_variant_builder_init(&o, G_VARIANT_TYPE_VARDICT);
    GVariant *r = g_dbus_connection_call_with_unix_fd_list_sync(
        C.bus, "org.freedesktop.portal.Desktop", "/org/freedesktop/portal/desktop",
        "org.freedesktop.portal.ScreenCast", "OpenPipeWireRemote",
        g_variant_new("(oa{sv})", C.session_handle, &o),
        NULL, G_DBUS_CALL_FLAGS_NONE, -1, NULL, &fds, NULL, &err);
    if (!r) {
        set_state(VIPSIM_FAILED, "OpenPipeWireRemote failed: %s", err ? err->message : "?");
        if (err) g_error_free(err);
        return -6;
    }
    gint32 idx = 0;
    g_variant_get(r, "(h)", &idx);
    int fd = g_unix_fd_list_get(fds, idx, NULL);
    g_variant_unref(r);
    g_object_unref(fds);

    return start_pipewire(fd, node_id) ? 0 : -7;
}

EXPORT int vipsim_capture_state(void) { return (int)C.state; }
EXPORT const char *vipsim_capture_message(void) { return C.message; }

EXPORT int vipsim_capture_frame_size(int *w, int *h)
{
    pthread_mutex_lock(&C.lock);
    if (w) *w = (int)C.width;
    if (h) *h = (int)C.height;
    int ok = C.frame && C.width && C.height;
    pthread_mutex_unlock(&C.lock);
    return ok ? 0 : -1;
}

/// Copy the newest frame out. Returns the sequence number, or 0 if there is nothing yet,
/// so the caller can skip the upload when nothing has changed.
EXPORT unsigned vipsim_capture_copy_frame(void *dst, int dst_stride)
{
    if (!dst) return 0;
    pthread_mutex_lock(&C.lock);
    unsigned seq = C.seq;
    if (C.frame && C.width && C.height) {
        uint32_t row = C.width * 4;
        for (uint32_t y = 0; y < C.height; y++)
            memcpy((char *)dst + (size_t)y * (size_t)dst_stride,
                   C.frame + (size_t)y * row, row);
    } else {
        seq = 0;
    }
    pthread_mutex_unlock(&C.lock);
    return seq;
}

EXPORT void vipsim_capture_stop(void)
{
    if (C.pw_loop) {
        pw_thread_loop_stop(C.pw_loop);
        if (C.pw_stream) { pw_stream_destroy(C.pw_stream); C.pw_stream = NULL; }
        if (C.pw_core) { pw_core_disconnect(C.pw_core); C.pw_core = NULL; }
        if (C.pw_context) { pw_context_destroy(C.pw_context); C.pw_context = NULL; }
        pw_thread_loop_destroy(C.pw_loop);
        C.pw_loop = NULL;
    }
    pthread_mutex_lock(&C.lock);
    free(C.frame); C.frame = NULL;
    C.width = C.height = C.seq = 0;
    pthread_mutex_unlock(&C.lock);

    if (C.session_handle) {
        g_dbus_connection_call(C.bus, "org.freedesktop.portal.Desktop", C.session_handle,
                               "org.freedesktop.portal.Session", "Close",
                               NULL, NULL, G_DBUS_CALL_FLAGS_NONE, -1, NULL, NULL, NULL);
        g_free(C.session_handle); C.session_handle = NULL;
    }
    set_state(VIPSIM_IDLE, "stopped");
}
