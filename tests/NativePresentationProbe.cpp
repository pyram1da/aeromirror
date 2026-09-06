#include "mainwindow.h"
#include <QApplication>
#include <QTimer>
#include <gst/gst.h>
#include <gst/app/gstappsrc.h>
#include <gst/video/videooverlay.h>
#include <windows.h>
#include <cstdio>

static BOOL CALLBACK inspectChild(HWND window, LPARAM) {
    char name[96] = {};
    GetClassNameA(window, name, sizeof(name));
    RECT client = {}, outer = {};
    GetClientRect(window, &client);
    GetWindowRect(window, &outer);
    POINT point = {client.right / 2, client.bottom / 2};
    HDC dc = GetDC(window);
    COLORREF pixel = GetPixel(dc, point.x, point.y);
    ReleaseDC(window, dc);
    ClientToScreen(window, &point);
    HWND hit = WindowFromPoint(point);
    COLORREF screenPixel = CLR_INVALID;
    const bool ownPoint = hit && GetAncestor(hit, GA_ROOT) == GetAncestor(window, GA_ROOT);
    if (ownPoint) {
        HDC screen = GetDC(nullptr);
        screenPixel = GetPixel(screen, point.x, point.y);
        ReleaseDC(nullptr, screen);
    }
    std::printf("child=%p parent=%p class=%s visible=%d client=%ldx%ld "
                "outer=%ld,%ld,%ld,%ld style=%lx center=%06lx own-screen=%d screen=%06lx\n",
                window, GetParent(window), name, IsWindowVisible(window),
                client.right, client.bottom, outer.left, outer.top,
                outer.right, outer.bottom, GetWindowLongA(window, GWL_STYLE),
                static_cast<unsigned long>(pixel), ownPoint, static_cast<unsigned long>(screenPixel));
    return TRUE;
}

int main(int argc, char **argv) {
    std::setvbuf(stdout, nullptr, _IONBF, 0);
    std::puts("before Qt initialization");
    QApplication app(argc, argv);
    const bool exerciseFullscreen = app.arguments().contains("--exercise-fullscreen");
    std::puts("before GStreamer initialization");
    gst_init(nullptr, nullptr);
    wchar_t loadedModule[MAX_PATH] = {};
    GetModuleFileNameW(GetModuleHandleA("libgstreamer-1.0-0.dll"), loadedModule, MAX_PATH);
    std::printf("GStreamer loaded from: %s\n", QString::fromWCharArray(loadedModule).toUtf8().constData());
    std::puts("GStreamer initialized");
    QMainWindow host;
    host.setWindowTitle("AeroMirror - synthetic presentation test (no phone)");
    host.setAttribute(Qt::WA_ShowWithoutActivating);
    auto *surface = new RendererVideoSurface(&host);
    host.setCentralWidget(surface);
    host.resize(547, 1184);
    host.move(70, 55);
    host.show();

    GError *error = nullptr;
    GstElement *pipeline = gst_parse_launch(
        "appsrc name=input is-live=true format=time "
        "caps=video/x-raw,format=BGRA,width=998,height=2160,framerate=30/1 "
        "! d3d11upload ! d3d11videosink name=output sync=false", &error);
    if (error || !pipeline) {
        std::fprintf(stderr, "%s\n", error ? error->message : "no pipeline");
        return 1;
    }
    GstElement *source = gst_bin_get_by_name(GST_BIN(pipeline), "input");
    GstElement *sink = gst_bin_get_by_name(GST_BIN(pipeline), "output");
    gst_video_overlay_set_window_handle(GST_VIDEO_OVERLAY(sink), surface->winId());
    g_object_set(sink, "force-aspect-ratio", TRUE, "fullscreen-toggle-mode", 0, nullptr);
    GstStateChangeReturn started = gst_element_set_state(pipeline, GST_STATE_PLAYING);
    std::printf("Qt=%s Gst=%s pipeline_start=%d host=%p surface=%p\n", qVersion(),
                gst_version_string(), started, reinterpret_cast<void *>(host.winId()),
                reinterpret_cast<void *>(surface->winId()));
    unsigned int frames = 0;
    QTimer framesTimer;
    QObject::connect(&framesTimer, &QTimer::timeout, [&]() {
        GstBuffer *buffer = gst_buffer_new_allocate(nullptr, 998 * 2160 * 4, nullptr);
        GstMapInfo mapped = {};
        gst_buffer_map(buffer, &mapped, GST_MAP_WRITE);
        for (gsize i = 0; i < mapped.size; i += 4) {
            mapped.data[i] = 255; mapped.data[i + 1] = 0;
            mapped.data[i + 2] = 255; mapped.data[i + 3] = 255;
        }
        gst_buffer_unmap(buffer, &mapped);
        GST_BUFFER_PTS(buffer) = frames * GST_SECOND / 30;
        GST_BUFFER_DURATION(buffer) = GST_SECOND / 30;
        GstFlowReturn pushed = gst_app_src_push_buffer(GST_APP_SRC(source), buffer);
        ++frames;
        if (pushed != GST_FLOW_OK) std::printf("push=%d\n", pushed);
    });
    framesTimer.start(33);
    QTimer::singleShot(2200, [&]() {
        std::printf("untouched-normal frames=%u\n", frames);
        EnumChildWindows(reinterpret_cast<HWND>(host.winId()), inspectChild, 0);
        std::fflush(stdout);
        // Stop new frames; later resize must display the same retained buffer.
        framesTimer.stop();
        gst_element_set_state(pipeline, GST_STATE_PAUSED);
    });
    if (!exerciseFullscreen) QTimer::singleShot(2500, [&]() { app.quit(); });
    if (exerciseFullscreen) QTimer::singleShot(2700, [&]() { host.showFullScreen(); });
    QTimer::singleShot(3400, [&]() {
        std::puts("fullscreen-paused");
        EnumChildWindows(reinterpret_cast<HWND>(host.winId()), inspectChild, 0);
        host.showNormal();
    });
    QTimer::singleShot(4100, [&]() {
        std::puts("restored-normal-paused");
        EnumChildWindows(reinterpret_cast<HWND>(host.winId()), inspectChild, 0);
        app.quit();
    });
    int result = app.exec();
    gst_element_set_state(pipeline, GST_STATE_NULL);
    gst_object_unref(source);
    gst_object_unref(sink);
    gst_object_unref(pipeline);
    return result;
}
