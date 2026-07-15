## What's New in v3.14.0

### New Features
- **BbDataGrid** — runtime grouping via the new `Groupable` parameter on property and template columns; a header ellipsis menu offers "Group by" / "Remove grouping", also available imperatively through `GroupByColumnAsync(columnId, direction)` and `ClearGroupingAsync()`.
- **BbDataGrid** — public `RefreshDataAsync()` to re-process the current data after in-place collection mutations, mirroring QuickGrid.
- **BbDataGrid** — `INotifyCollectionChanged` support: an `ObservableCollection` passed to `Items` refreshes the grid automatically, with subscriptions cleaned up on dispose and source swap.
- **BbSidebarProvider** — new `EnableToggleShortcut` parameter (default `true`) to opt out of the Ctrl/Cmd+B sidebar shortcut so the keys can reach the page (e.g. a rich-text editor's bold command); reactive after first render.

### Bug Fixes
- **BbDataGrid** — grouping set programmatically through the state object now actually applies and round-trips through `Save()`/`Restore()`; previously only markup-configured grouping took effect while the state reported a grouping the grid ignored.
- **BbDataGrid** — group definitions targeting a column that registers later no longer silently resolve to nothing, and `Reset()` now genuinely clears grouping applied via the `GroupBy` parameter.
- **Theming** — the applied theme (dark mode class, base/primary color attributes, radius) is now restored when Blazor's enhanced page refresh re-merges the document and strips it from `<html>` (e.g. on every `dotnet watch` hot reload); the dark-mode toggle no longer needs two clicks afterwards.
- **CSS** — border color utilities (`border-primary`, `border-alert-*/30`, and consumer `.border-*` classes) are no longer flattened to the default border color; the global border reset moved from the `bb` cascade layer to `base` so utilities win again.
- **BbSidebar** — multiple `BbSidebarProvider` instances on one page now each receive the toggle shortcut and mobile-change notifications; previously module-level JS state meant only the last-initialized provider responded and disposal leaked listeners.

### Improvements
- **BbDataGrid** — grouping combined with `Virtualize` + `ItemsProvider` (unsupported) now hides the header group action and logs a warning naming the column instead of silently rendering an empty grid.
- Bumped the `BlazorBlueprint.Primitives` dependency to 3.14.0.
