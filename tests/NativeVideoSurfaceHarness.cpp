#include "mainwindow.h"
#include <QApplication>
#include <QCoreApplication>
#include <cstdio>
#include <cstdlib>
#include <cstring>

static void require(bool condition, const char *message) {
    if (!condition) {
        std::fprintf(stderr, "FAILED: %s\n", message);
        std::exit(1);
    }
}

int main(int argc, char **argv) {
    QApplication app(argc, argv);
    require(std::strcmp(qVersion(), "6.10.1") == 0, "pinned Qt runtime");
    require(QApplication::platformName() == "windows", "real Windows plugin");

    // Reproduce the framework contract, without showing any window or receiver.
    QWidget baseline;
    baseline.setAttribute(Qt::WA_NativeWindow);
    baseline.setAttribute(Qt::WA_PaintOnScreen);
    require(!baseline.testAttribute(Qt::WA_PaintOnScreen),
            "base QWidget rejects PaintOnScreen on Windows");

    QMainWindow host;
    auto *surface = new RendererVideoSurface(&host);
    host.setCentralWidget(surface);
    require(host.winId() && surface->winId(), "real host and child HWND exist");
    require(host.winId() != surface->winId(), "surface is a separate HWND");
    require(surface->testAttribute(Qt::WA_PaintOnScreen), "PaintOnScreen retained");
    require(surface->testAttribute(Qt::WA_NoSystemBackground), "no background erase");
    require(surface->testAttribute(Qt::WA_OpaquePaintEvent), "opaque external surface");
    require(!surface->autoFillBackground(), "no Qt auto-fill");
    require(surface->paintEngine() == nullptr, "no Qt paint engine");
    surface->update();
    host.update();
    QCoreApplication::processEvents();
    require(surface->testAttribute(Qt::WA_PaintOnScreen), "contract survives events");
    require(!host.isVisible() && !baseline.isVisible(), "test shows no windows");
    std::puts("Native video surface checks passed (Qt 6.10.1, Windows, hidden HWNDs).");
    return 0;
}
