## What's New in v3.6.0

### New Components

- **BbSortable\<TItem\>** — Headless drag-and-drop sortable primitive powered by SortableJS, with full JS interop lifecycle, ARIA live announcements, and stable Blazor diffing via automatic `@key` wrappers

### New Features

- **Connected multi-list sorting** — Share a `Group` name across multiple `BbSortable` instances to enable cross-list drag-and-drop via `OnAdd` and `OnRemove` callbacks
- **Drag handles** — Use the `Handle` parameter with a CSS selector to restrict drag initiation to a specific child element
- **Item filtering** — Use the `Filter` parameter to exclude specific child elements from being draggable
- **Dynamic re-initialization** — Changing configuration parameters (`Group`, `Pull`, `Put`, `Sort`, `Handle`, `Filter`, `ForceFallback`) at runtime automatically destroys and re-creates the JS instance

### Bug Fixes

- **Sortable grid oscillation** — Added swap debounce to prevent SortableJS from immediately reversing swaps between adjacent grid items, eliminating the visible "dancing" effect during drag
