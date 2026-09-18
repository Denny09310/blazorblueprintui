## What's New in v4.0.0-beta.8

**This is a prerelease.** The API may still change before the stable v4.0.0 release.

### Breaking Changes

- **.NET 10**: the package now targets `net10.0` only and depends on `Microsoft.AspNetCore.Components.Web` 10.0.12. .NET 8 and .NET 9 are no longer supported.
- **BbPopoverContent**, **BbSelectContent**, **BbDropdownMenuContent**: the `JsOnClickOutside` JSInvokable method is removed, and **BbPopoverContent** also drops `JsOnEscapeKey`. Dismissal now arrives through the new `BbFloatingPortal.OnDismiss` callback.
- **BbTableRow** and **BbDataGridRow** no longer implement `IAsyncDisposable`. Code that awaited `DisposeAsync()` on these components must be updated.
- **BbFloatingPortal** opens and closes from `OnParametersSet` without awaiting interop. It no longer passes the floating element to JavaScript; the content is located by a `data-bb-portal` attribute instead.
- **BbFloatingPortal**: JavaScript now owns the resolved `data-side` attribute and a listbox's `data-focused` and `aria-activedescendant` state. Owners that rendered these from C# must stop, or the two writers will conflict.
- **click-outside.js**: the `onClickOutside` and `onEscapeKey` exports are removed. Use `BbFloatingPortal` with `FloatingDismissOptions` instead.
- **BbTableRow**, **BbDataGridRow**, **BbMenubarContent**, **BbSortable**: the Tailwind utilities these primitives render (row focus ring, menubar backdrop, sortable `sr-only` live region) are now `bb:`-prefixed. A consumer's own Tailwind build no longer emits them, so remove any `@source` that points at the library and update CSS or test selectors that matched the old class names.
- **BbSortable**: keyboard sorting is on by default, so each item, or its drag handle, is now a tab stop. Set `KeyboardSorting="false"` to keep the previous tab order.
- **BbSortable**: in a cross-list drop between two `BbSortable` lists, the source list's `OnRemove` now runs before the target list's `OnAdd`. Code that relied on the old order must be updated.

### New Components

- **BbMenuSub**, **BbMenuSubTrigger** and **BbMenuSubContent**: nested submenus for dropdown menus, context menus and menubars. The trigger opens on hover or click, and the arrow keys move into and back out of the submenu, including in right-to-left layouts.
- **BbMenuRadioGroup** and **BbMenuRadioItem**: generic single-choice items inside any menu, with `Value`, `ValueChanged` and `CloseOnSelect`.
- **BbContextMenuCheckboxItem**: a checkbox item for context menus, with `Checked`, `CheckedChanged` and `CloseOnSelect`.

### New Features

- **Native dialog rendering**: **BbDialog** gains a `RenderingStrategy` parameter. Set it to `OverlayRenderingStrategy.Native` to render a browser `<dialog>` element driven by `showModal()`, which works across Blazor render-mode boundaries and does not need a portal host. When the browser lacks `showModal()` support, a warning is logged and the dialog does not render as a modal.
- **OverlayRenderingOptions**: `AddBlazorBlueprintPrimitives` now accepts a configure callback to set a global `DefaultStrategy` for all overlays.
- **INativeOverlayService**: new scoped service that resolves the effective rendering strategy and drives the native `<dialog>` element (show, close, focus, and lifecycle events).
- **BbDialogContent** gains `CloseOnOverlayClick` to control whether a backdrop click closes a native dialog.
- **BbFloatingPortal** gains `Dismiss` (`FloatingDismissOptions`) and `OnDismiss` (`EventCallback<FloatingDismissReason>`), so the portal wires outside-click and Escape dismissal in the same call that opens the overlay.
- **BbFloatingPortal** gains `Keyboard` (`FloatingKeyboardOptions`). Listbox or menu keyboard handling is wired inside the open call, with `FloatingKeyboardKind` selecting the behaviour.
- **BbFloatingPortal** gains `SideElementId`, so JavaScript writes the resolved `data-side` attribute on a named element without a C# re-render.
- **BbFloatingPortal** gains `ScrollToCurrentIn` and `ScrollToCurrentSelector`, which scroll a chosen item into view before the overlay is revealed.
- **FloatingKeyboardOptions** gains `InitialFocus` (`"first"`, `"last"` or `"container"`), which moves focus into a menu after the overlay is revealed.
- **BbFloatingPortal** gains `AutoFocusId`, which focuses a named element one frame after the reveal, inside the call that opens the overlay.
- **BbFloatingPortal** gains `RestoreFocusToId` and `RestoreFocusOnClose`, so the browser returns focus to the trigger inside the close call for an intentional close only.
- **BbPopoverContent** gains `AutoFocusId`, `ScrollToSelected` and `ScrollToSelectedSelector`, so a popover-based list can focus its search box and open already scrolled to its current item.
- **DataGridEditMode** adds `Cell` (edit one cell in an isolated draft) and `Batch` (stage cell edits across rows until the batch is saved or discarded).
- **DataGridEditBuffer<TData>**: stages independently cloned rows as `DataGridEditChange<TData>` entries, keyed by a stable row key or by reference identity, without modifying the source records.
- **DataGridRowSnapshot<TData>.ApplyTo** copies captured property values onto another record. Unlike `Restore`, setter failures propagate.
- **bb-primitives.js**: all primitive JavaScript modules are bundled and re-exported under a namespace each (`focusTrap.createFocusTrap`, `positioning.computePosition`, etc.).
- **JsModules.GetAsync** and **PrimitiveModules.GetAsync**: shared, per-circuit module references that any component can use without owning or disposing them. **JsModules.TryGetLoaded** and **PrimitiveModules.TryGetLoaded** return an already-loaded module synchronously.
- **JsModules.Versioned** appends an assembly's informational version to a module path as a `v` query, so a new release is a new URL. **PrimitiveModules.ModuleUrl** exposes the versioned bundle URL.
- **elementUtils.observeNearBottom** and **observeHover**: new JavaScript observers that call .NET once when a list scrolls near its bottom or when the pointer moves onto a different item.
- **BbSortable keyboard sorting**: Space or Enter picks up an item, the arrow keys, Home and End move it, Space or Enter drops it, and Escape cancels. Ctrl plus Left or Right moves the item to the next connected list. `KeyboardInstructions` sets the accessible instructions linked to each handle.
- **BbSortable** gains `CanMove` and `CanDrop`, which reject a reorder or a cross-list drop before any list callback runs.
- **BbSortable** gains `DragOverlayTemplate`, a decorative preview that follows the pointer during a drag. Setting it turns on the fallback renderer.
- **BbSortable**: when `Handle` is not set, an element marked `data-bb-sortable-handle` inside an item becomes the drag handle. A disabled handle cannot start a drag.
- **BbToggleGroup** gains `Required`, which stops the user from clearing the last selected value.
- **Theme scopes**: an overlay or a sortable drag preview opened from inside an element marked `data-bb-theme-scope` copies that element's theme CSS variables and font, and follows changes to them while open.
- **Tree keyboard navigation**: in a tree marked `data-tree-select="true"`, Space expands or collapses a branch without changing the value, and Enter selects the item or toggles its checkbox.

### Bug Fixes

- **Overlays**: Escape now closes only the topmost open overlay. A popover inside a dialog no longer closes the dialog on the first press.
- **Overlays**: the close waits only for the overlay's own finite exit animation. A spinner or a child transition (tree chevron, hovered row, checkbox) no longer makes a closed popup reappear briefly.
- **Overlays**: navigating away from an open select, popover or menu no longer logs `System.ArgumentException: There is no tracked object`.
- **Overlays**: focus returns to the trigger on close even when a composed trigger supplies its own `id`.
- **BbFloatingPortal**: removed the fixed 500ms deadline on the portal host render signal, which timed out on slow connections. The wait is now unbounded and cancelled on close or disposal.
- **BbFloatingPortal**: a listbox is focused only after the reveal, so arrow keys no longer reach the trigger while the overlay is still hidden.
- **BbSelectContent**: hover and keyboard highlight are written by JavaScript only, so two options can no longer appear focused at once.
- **BbSelectTrigger**: opening the select with Enter or Space no longer closes it again on the native click that follows.
- **BbDropdownMenuContent**: clicking a nested portal inside an open menu no longer closes the menu. Outside-click detection now resolves elements by id per event, so it does not go stale after a re-render.
- **BbPopoverContent** and **BbDropdownMenuContent** no longer render a second time on open. The duplicate render raised a portal refresh mid-cycle that the host had to defer by a round trip.
- **BbDataGridRow**: controls inside an editing row now receive arrow keys and other key events, and row navigation shortcuts pause while the row is being edited.
- **BbDataGridRow**: pressing Enter on a button, picker trigger, checkbox or switch inside an editing row no longer also commits the row.
- **BbSlider**: right and middle clicks no longer start a drag, and a lost pointer capture now ends the drag.
- **elementUtils.focusElement** waits for the element to become visible before focusing it, so focus reaches portal content that is revealed after positioning, such as a nested calendar.
- **Tree view keyboard navigation** skips items marked `hidden` and keeps a tabbable item when filtering hides the previous tab stop.
- **Focus trap**: Tab no longer leaves a modal when focus is on the container or on a listbox outside the tab order, which WebKit allowed. With nothing focusable inside, Tab keeps focus on the container.
- **Menu keyboard navigation**: a parent menu no longer handles keys pressed inside a nested menu, and keys pressed with Ctrl, Alt or Meta are ignored. In a right-to-left menubar, Left and Right now move to the correct menu.
- **BbToggleGroup**: without a bound value, items now show the new pressed state as soon as they are toggled.
- **BbSortable** no longer calls `OnUpdate` for a move with an out-of-range or unchanged index, or when `Sort` is false.
- **JavaScript modules**: a browser or CDN that serves a stale copy of a bundled module no longer kills the circuit at the first call. The bundle fails at load with an error that names the file and says what to do.
- **NavigationMenuContext**: the close timer catches every exception, so an unexpected error in the fire-and-forget handler can no longer close the Blazor Server circuit.

### Improvements

- **Package licensing**: the NuGet package now includes `LICENSE`, `NOTICE` and `THIRD-PARTY-NOTICES.txt` (also served at `_content/BlazorBlueprint.Primitives/THIRD-PARTY-NOTICES.txt`), and the bundled Floating UI file carries its MIT license header.
- **README** documents the JavaScript bundle, the `overlay.open` pattern, the rules for adding a primitive that needs JavaScript, and the bundled license notices.
- **BbSortable** renders each item with `role="listitem"` and a `data-bb-sortable-item` attribute, and gives its live status region and keyboard instructions stable ids.

### Performance

- **Overlays** open and close in one circuit round trip each. The portal registers content, positions, reveals, starts auto-update, and wires dismissal and keyboard listeners in a single interop call that is not awaited. On a 20ms round trip, a select open went from 11 messages to 1 and a close from 16 to 1 compared with v3.
- **Overlays**: arrow keys and option hover in a listbox no longer send a message to the server.
- **Overlays**: an element named by `AutoFocusId` is focused inside the open call, replacing a ready callback, a 50ms sleep, and a second round trip.
- **JavaScript modules** are imported once per circuit and shared, instead of once per component instance. Pages with many inputs issue far fewer round trips on Blazor Server.
- **Floating UI** is statically imported by `positioning.js`, removing a hidden dynamic import on the first position computation.
- **BbDataGridRow** and **BbTableRow** no longer attach keyboard and click handlers per row. **BbDataGrid** and **BbTable** delegate a single handler from the container, and rows opt in via `data-bb-row-keys` and `data-bb-row-click`.
- **BbSelectContent** scrolls the selected option into view and attaches the keyboard handler in the same call that opens the overlay, so the list appears already scrolled to the selection.
- **BbSelectContent**, **BbPopoverContent** and **BbDropdownMenuContent** restore focus to the trigger inside `overlay.close` instead of awaiting `FocusAsync` after the close render, saving a round trip on every Escape and every selection.
- **BbDropdownMenuContent** drops a redundant width-matching interop call; `MatchAnchorWidth` already covers it.
- **BbTooltipContent** and **BbHoverCardContent** no longer request the portal's ready callback, which was empty and cost a round trip on Blazor Server.
- **BbPopoverContent** requests the portal's ready callback only when a consumer has set `OnContentReady`.
- **BbSlider**: the drag preview stays in the browser through a `--bb-slider-position` CSS variable. Only the latest distinct snapped value is sent to .NET, at most every 50ms with one call in flight.
- **BbSlider** no longer raises `ValueChanged` or re-renders when the snapped value has not changed.
- **Scroll containers**: `observeNearBottom` checks the scroll position in the browser, coalesced to one check per frame, and calls .NET once on entering the near-bottom zone instead of once per scroll event.
- **Lists**: `observeHover` uses one delegated hover listener per list and reports only a genuine change of item, so pointer travel no longer sends a message per pixel.
