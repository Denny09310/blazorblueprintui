// Bar drags stay in the browser. Only the finished drag crosses the Blazor circuit, because a
// pointermove per frame over a server circuit is a bar that lags behind the pointer.
const instances = new WeakMap();

/**
 * Scroll the timeline so that a content position sits in the middle of the visible timeline.
 * @param {HTMLElement} root - The scrolling container.
 * @param {number} x - The position, measured from the content's start edge.
 * @param {number} stickyWidth - How much of the viewport the task list covers.
 */
export function scrollTo(root, x, stickyWidth) {
    if (!root) return;
    // The task list sits over the first stickyWidth pixels, so the timeline the reader can
    // actually see is narrower than the container by exactly that much.
    const visible = Math.max(0, root.clientWidth - stickyWidth);
    root.scrollLeft = Math.max(0, x - stickyWidth - visible / 2);
}

/**
 * Wire up dragging and resizing of bars.
 * @param {HTMLElement} root - The chart's scrolling container.
 * @param {object} dotNet - The Blazor component reference.
 */
export function initialize(root, dotNet) {
    if (!root || !dotNet) return;
    if (instances.has(root)) {
        instances.get(root).dotNet = dotNet;
        return;
    }

    const state = { dotNet, gesture: null, suppressClickUntil: 0 };
    instances.set(root, state);

    const allowed = action =>
        root.dataset[action === 'move' ? 'allowDrag' : 'allowResize'] === 'true';

    const restore = gesture => {
        gesture.bar.style.left = gesture.left;
        gesture.bar.style.width = gesture.width;
    };

    const slotsMoved = (gesture, clientX) => {
        const slotWidth = Number(root.dataset.slotWidth) || 1;
        const raw = (clientX - gesture.startX) / slotWidth;
        return root.dataset.snap === 'true' ? Math.round(raw) : raw;
    };

    const onDown = event => {
        if (event.button !== 0 || state.gesture) return;

        const bar = event.target.closest('[data-gantt-bar]');
        if (!bar || !root.contains(bar)) return;

        const action = event.target.closest('[data-gantt-resize]')?.dataset.ganttResize ?? 'move';
        if (!allowed(action)) return;

        event.preventDefault();

        state.gesture = {
            bar,
            action,
            id: bar.dataset.ganttBar,
            pointerId: event.pointerId,
            startX: event.clientX,
            // The inline styles as Blazor wrote them. A refused drag has to put them back
            // verbatim: Blazor only patches what it believes changed, and it does not know this
            // file touched anything.
            left: bar.style.left,
            width: bar.style.width,
            originalLeft: parseFloat(bar.style.left) || 0,
            originalWidth: parseFloat(bar.style.width) || 0,
            moved: false,
        };

        try { bar.setPointerCapture(event.pointerId); } catch { /* capture is a nicety */ }
    };

    const onMove = event => {
        const gesture = state.gesture;
        if (!gesture || event.pointerId !== gesture.pointerId) return;

        const slotWidth = Number(root.dataset.slotWidth) || 1;
        const offset = slotsMoved(gesture, event.clientX) * slotWidth;
        if (offset !== 0) gesture.moved = true;

        if (gesture.action === 'move') {
            gesture.bar.style.left = `${gesture.originalLeft + offset}px`;
        } else if (gesture.action === 'start') {
            // Never past the far edge: a bar dragged inside out is a task that finishes before it
            // starts, and the reader cannot see what they are asking for.
            const width = Math.max(2, gesture.originalWidth - offset);
            gesture.bar.style.left = `${gesture.originalLeft + gesture.originalWidth - width}px`;
            gesture.bar.style.width = `${width}px`;
        } else {
            gesture.bar.style.width = `${Math.max(2, gesture.originalWidth + offset)}px`;
        }
    };

    const onUp = event => {
        const gesture = state.gesture;
        if (!gesture || event.pointerId !== gesture.pointerId) return;

        state.gesture = null;
        try { gesture.bar.releasePointerCapture(event.pointerId); } catch { /* already gone */ }
        restore(gesture);

        if (!gesture.moved) return;

        // A drag that ends on the bar would otherwise also read as a click on it.
        state.suppressClickUntil = Date.now() + 300;
        state.dotNet
            .invokeMethodAsync('OnBarDragged', gesture.id, gesture.action, slotsMoved(gesture, event.clientX))
            .catch(() => { /* the component may be gone */ });
    };

    const onCancel = () => {
        if (!state.gesture) return;
        restore(state.gesture);
        state.gesture = null;
    };

    const onClick = event => {
        if (Date.now() < state.suppressClickUntil) {
            event.stopPropagation();
            event.preventDefault();
        }
    };

    root.addEventListener('pointerdown', onDown);
    root.addEventListener('pointermove', onMove);
    root.addEventListener('pointerup', onUp);
    root.addEventListener('pointercancel', onCancel);
    root.addEventListener('click', onClick, true);

    state.teardown = () => {
        root.removeEventListener('pointerdown', onDown);
        root.removeEventListener('pointermove', onMove);
        root.removeEventListener('pointerup', onUp);
        root.removeEventListener('pointercancel', onCancel);
        root.removeEventListener('click', onClick, true);
    };
}

/**
 * Drop the handlers for a chart.
 * @param {HTMLElement} root - The chart's scrolling container.
 */
export function dispose(root) {
    const state = root && instances.get(root);
    if (!state) return;
    state.teardown?.();
    instances.delete(root);
}
