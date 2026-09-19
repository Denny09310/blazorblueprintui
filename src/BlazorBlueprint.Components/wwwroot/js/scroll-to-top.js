/**
 * Watches a scroll container and reports when it has passed a threshold, so the button can
 * appear only once there is something to scroll back from.
 *
 * The listener is passive and the reported state is de-duplicated, so a fast scroll crosses the
 * circuit once per change rather than once per frame.
 */
const watchers = new Map();

function resolveTarget(selector) {
    if (!selector) {
        return { element: document.scrollingElement || document.documentElement, listenOn: window };
    }
    const element = document.querySelector(selector);
    return element ? { element, listenOn: element } : null;
}

export function observe(id, selector, threshold, dotNetRef) {
    dispose(id);

    const target = resolveTarget(selector);
    if (!target) {
        return false;
    }

    let visible = null;
    const report = () => {
        const next = target.element.scrollTop > threshold;
        if (next === visible) {
            return;
        }
        visible = next;
        dotNetRef.invokeMethodAsync('JsSetVisible', next).catch(() => {});
    };

    target.listenOn.addEventListener('scroll', report, { passive: true });
    report();

    watchers.set(id, () => target.listenOn.removeEventListener('scroll', report));
    return true;
}

export function scrollToTop(selector, smooth) {
    const target = resolveTarget(selector);
    if (!target) {
        return;
    }
    // Honour a reduced-motion preference rather than animating regardless.
    const reduced = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    target.element.scrollTo({ top: 0, behavior: smooth && !reduced ? 'smooth' : 'auto' });
}

export function dispose(id) {
    watchers.get(id)?.();
    watchers.delete(id);
}
