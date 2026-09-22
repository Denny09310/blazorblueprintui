// Pdfjs interop for PdfViewer component
// Handles PDF loading, rendering and lifecycle.

import * as pdfjsLib from "../lib/pdfjs/pdf.mjs";

const workerUrl = new URL(
    "../lib/pdfjs/pdf.worker.mjs",
    import.meta.url
);

pdfjsLib.GlobalWorkerOptions.workerSrc = workerUrl.href;

const viewers = new WeakMap();

export async function load(canvas, url) {
    await dispose(canvas);

    const loadingTask = pdfjsLib.getDocument({
        url
    });

    const pdf = await loadingTask.promise;

    viewers.set(canvas, {
        pdf,
        currentPage: 1,
        scale: 1.5
    });

    await renderPage(canvas, 1);
}

async function renderPage(canvas, pageNumber) {
    const viewer = viewers.get(canvas);

    if (!viewer) {
        return;
    }

    const page = await viewer.pdf.getPage(pageNumber);

    const viewport = page.getViewport({
        scale: viewer.scale
    });

    const devicePixelRatio = window.devicePixelRatio || 1;

    canvas.width = Math.floor(
        viewport.width * devicePixelRatio
    );

    canvas.height = Math.floor(
        viewport.height * devicePixelRatio
    );

    canvas.style.width = `${viewport.width}px`;
    canvas.style.height = `${viewport.height}px`;

    const context = canvas.getContext("2d");

    const transform =
        devicePixelRatio !== 1
            ? [
                devicePixelRatio,
                0,
                0,
                devicePixelRatio,
                0,
                0
            ]
            : null;

    await page.render({
        canvasContext: context,
        viewport,
        transform
    }).promise;
}

export async function clear(canvas) {
    await dispose(canvas);

    const context = canvas.getContext("2d");

    if (context) {
        context.clearRect(
            0,
            0,
            canvas.width,
            canvas.height
        );
    }

    canvas.width = 0;
    canvas.height = 0;
}

export async function dispose(canvas) {
    const viewer = viewers.get(canvas);

    if (!viewer) {
        return;
    }

    viewers.delete(canvas);

    try {
        await viewer.pdf.destroy();
    }
    catch {
        // Ignore PDF.js cleanup errors.
    }
}