## What's New in v3.5.0

### New Components

- **BbDataGridHierarchyColumn** — Hierarchical tree data grid with expand/collapse, depth-based indentation, nested and self-referencing data modes, lazy loading, per-level sorting, and child pagination
- **IBbLocalizer** — Localization interface with `DefaultBbLocalizer` implementation, enabling `IStringLocalizer` integration and customization of all component chrome strings

### New Features

- **DialogService** — Alert, Prompt, and custom Component dialogs via `AlertAsync`, `PromptAsync`, and `OpenAsync<T>` methods with typed results, escape key handling, focus trap, scroll lock, and dynamic sizing
- **DataGrid** — `HierarchySelectionMode.Cascade` for parent-child checkbox cascading with indeterminate state
- **DataGrid** — `HierarchyFilterMode` enum (`ShowMatchedSubtree` / `ShowMatchedOnly`) to control how filtered results display in tree structures
- **FilterBuilder** — Added `Hours`, `Minutes`, and `Seconds` options to `InLast`/`InNext` date filter periods
- **BbDateRangePicker** — `Clear` method changed to public visibility for programmatic clearing
- **BbInput** — Added `AdditionalAttributes` parameter for parity with other input components

### Bug Fixes

- **DataGrid** — Fixed column filtering for nullable types, empty result visibility (header stays visible), and `DateTimeOffset` support; auto-generate enum filter options from CLR type
- **DataGrid** — Respect initial `Visible` property on columns so `Visible=false` works on first render
- **InputOtp** — Prevent invalid characters based on `InputMode` (numeric, alphanumeric, etc.)
- **Textarea** — Corrected `UpdateTiming.OnChange` documentation to not mention Enter key (fires on blur only)
- **Combobox/MultiSelect** — Fixed cascading re-render blocking from `ShouldRender()` optimization; reset JS state on setup failure
- **DateRangePicker/RichTextEditor/TimePicker/Calendar** — Fixed parent re-render cascading through optimized components
- **ToastService** — Added thread-safe locking to list operations
- **TagInput** — Fixed `ShouldRender()` to return false when no changes detected

### Improvements

- **TailwindMerge** — Complete rewrite with correct conflict resolution: variant prefix scoping, shorthand-to-longhand handling, arbitrary value support, negative values, important modifier, 40+ new utility groups, bounded cache, and dictionary-based lookups replacing regex
- **DialogService** — Proper ARIA roles (`alertdialog`/`dialog`), `aria-labelledby`/`aria-describedby`, stacked dialog focus trap management
- **Separator/Toggle/ToggleGroup/Progress/AlertDialog/Slider/ContextMenu** — Refactored to delegate behavior and accessibility to new Primitives layer
- **JS interop resilience** — Added `JSException` catch filters across all overlay components for graceful offline/disconnect handling
- **InputConverter** — Respects `CultureInfo.CurrentCulture` for number parsing

### Performance

- **TailwindMerge** — Dictionary lookups and progressive prefix matching replace regex-based matching; bounded LRU cache prevents memory growth in long-running server sessions
- **BbTooltipTrigger** — Cached `TriggerContext` to avoid allocating a new instance per render
