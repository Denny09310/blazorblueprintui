## What's New in v4.1.0 (unreleased)

> **Prerelease preparation:** these cumulative notes describe the current v4.1.0 branch. The release script selects the package version.

### Breaking Changes

- **BbTabsTrigger**: Ctrl or Cmd with an arrow key, Home or End no longer moves focus to another tab. With an arrow key it now asks to move the tab through `OnMoveRequested`, and does nothing when that callback is not set.
- **BbTooltipTrigger** and **BbHoverCardTrigger**: the Development-only AsChild warning changes its event name from `TooltipTriggerContextUnconsumed` and `HoverCardTriggerContextUnconsumed` to `TriggerContextUnconsumed`, and its message text changes. Update any log filter that matched the old names.

### New Components

- **BbSwipeArea**: a headless swipe gesture primitive. Wrap any content to get `OnSwipe` with direction, distance and velocity, plus `OnSwipeMove`, `OnSwipeCancel` and `CancelAsync()`. `Threshold`, `MinVelocity`, `Axis` and `TouchAction` tune the gesture, and the gesture maths runs in the browser, so a swipe costs one call into .NET.
- **BbSignaturePad**: a drawing surface for signatures. The line tapers with pen speed (`MinWidth`, `MaxWidth`, `VelocityWeight`), and the ink follows the theme unless `StrokeColor` is set. It exports SVG, PNG and raw strokes (`GetSvgAsync`, `GetPngAsync`, `GetPngBytesAsync`, `GetStrokesAsync`), and supports `ClearAsync`, `UndoAsync` and `SetStrokesAsync`. Exports are streamed, so a large signature does not exceed the Blazor Server message size limit.

### New Features

- **QrEncoder**: a QR code encoder written from scratch, with no JavaScript and no dependency. It covers versions 1 to 40, all four error correction levels (`QrErrorCorrection`), and numeric, alphanumeric and UTF-8 byte modes (`QrEncodingMode`). `GetCapacity` reports how much a version holds.
- **BarcodeEncoder**: encoders for 14 barcode types (`BarcodeType`): Code 128, Code 39, EAN-13, EAN-8, UPC-A, Interleaved 2 of 5, Codabar, ISBN, ISSN, MSI, Telepen, Pharmacode, POSTNET and Royal Mail 4-state. Each type checks its own characters, length and check digit, and a bad value throws `BarcodeFormatException` with a message that is safe to show to the user.
- **GanttBuilder**: the timeline engine behind BbGantt, with no markup. It builds the task tree, rolls up summary dates and progress, lays out the time axis at six zoom levels (`GanttZoom`), and resolves the four dependency types (`GanttDependencyType`).
- **PivotBuilder**: the cross-tabulation engine behind BbPivotDataGrid, with no markup. Totals and subtotals (`PivotTotals`) are computed from every item under them, so an average total is a true average.
- **BbTabsTrigger** gains `OnCloseRequested` (Delete or Backspace), `OnRenameRequested` (F2) and `OnMoveRequested` (Ctrl or Cmd with an arrow key). Each key does nothing until its callback is set, and the move follows the writing direction.
- **AsChildDiagnostics** and **AsChildTriggerDescription**: public helpers, so a custom AsChild trigger can log the same Development-only warning as the library's own triggers.

### Bug Fixes

- **BbDialogClose**, **BbSheetClose**: native keyboard activation invokes the close action once, including when closing is prevented.
- **Dialog and Popover**: `Modal` now controls outside/Escape dismissal according to its existing contract; it does not change focus trapping.
- **BbMenubar**: closed triggers support keyboard navigation, Escape restores focus, and outside-pointer handling no longer uses a blocking overlay.
- **Navigation menus**: optional arrow/Home/End/Escape navigation works, links remain tabbable, and items without explicit values receive stable IDs instead of opening on a null match.
- **BbContextMenu**: controlled `Open` values are observed, and uncontrolled state changes notify subscribed callbacks.
- **Dropdown menus**: `Dir` applies to trigger content and portaled panels; a null value inherits surrounding direction.
- **BbSignaturePad**: restoring strokes reports whether the filtered drawing is actually empty. Clearing an already-empty pad retains the documented successful-operation callback.

### Improvements

- **Sortable**: per-message overrides make headless keyboard instructions and announcements localizable.

- **AsChild triggers**: **BbCollapsibleTrigger**, **BbPopoverTrigger**, **BbDialogTrigger**, **BbDialogClose**, **BbSheetTrigger**, **BbSheetClose** and **BbDropdownMenuTrigger** now log a Development-only warning when nothing inside them reads the `TriggerContext`. Before, text or an icon inside such a trigger did nothing, and nothing said why.
- **AsChild warning**: all triggers share one message, which says to set `AsChild="false"` or to put a BbButton inside, and names any class or attributes that had no element to go on.
- **bb-primitives.js** now bundles the `signaturePad` and `swipeArea` modules, and `PrimitiveModules.ModuleUrl` changes so browsers fetch the new bundle.
