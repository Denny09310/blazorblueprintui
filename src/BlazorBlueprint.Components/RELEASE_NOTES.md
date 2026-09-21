## What's New in v4.1.0-beta.1

> **Prerelease:** this is a beta of v4.1.0, and the API may still change before the stable release.

### Breaking Changes

- **BlazorBlueprint.Primitives**: the dependency is now 4.1.0-beta.1. Keep Components and Primitives on matching versions, and see the Primitives release notes.
- **BbTabsTrigger**: Ctrl or Cmd with an arrow key, Home or End no longer moves focus to another tab. Ctrl or Cmd with an arrow key now moves the tab, and only when **BbTabsList** has `Reorderable` on and `OnMove` set. Otherwise it does nothing.
- **BbTooltipTrigger**, **BbHoverCardTrigger**: the Development-only AsChild warning now logs under the event name `TriggerContextUnconsumed` instead of `TooltipTriggerContextUnconsumed` and `HoverCardTriggerContextUnconsumed`, and its text changes. Update any log filter that matched the old names.
- **BbDataGrid**: the column resize module `js/datagrid-columns.js` is renamed `js/table-columns.js`, and each `<col>` now carries `data-column-id`. Only custom code that imported the old file directly needs to change.

### New Components

- **BbGantt**, **BbGanttColumn**: a plan drawn against a timeline, with a task list, a bar per task and dependency arrows. It has six zoom levels, summary tasks that roll up dates and progress from their children, milestones, non-working-day shading and a today line. The chart draws dependencies but does not enforce them, so moving a task does not move the tasks after it.
- **BbGantt** editing: `AllowDrag`, `AllowResize`, `AllowProgressDrag`, `AllowLinking` and `AllowRowDrag` hand each change back through `OnTaskChange`, `OnDependencyCreate` and `OnTaskMove` rather than writing it, and each change can be refused. `ShowLegend`, `ShowTooltip`, sortable and resizable columns, and right-to-left pages are supported.
- **BbPivotDataGrid**, **BbPivotField**, **BbPivotValue**: row fields down the side, column fields across the top, and an aggregate where they cross. Totals and subtotals are worked out from every item under them, so an average total is a true average. `OnCellClick` gives the items behind a cell, `ShowFieldPicker` turns fields on and off, and paging never splits a row group.
- **BbQrCode**: a QR code drawn as SVG in the markup, with no JavaScript and no image request. It supports four error correction levels, `ModuleShape`, a centre `Image`, `ShowValue` and `AriaLabel`. The colours stay dark on light whatever the theme, and a value that is too long shows a message instead of throwing.
- **BbBarcode**: 14 barcode types drawn as SVG, including Code 128, Code 39, EAN-13, UPC-A, ISBN and POSTNET. Each type checks its characters, length and check digit, and an invalid value shows a message naming the problem instead of throwing. `ShowValue` prints the value as selectable text.
- **BbSignature**: sign by drawing or by typing a name. `@bind-Value` gives a `SignatureValue` with `Kind`, `Svg` and `Text`, and `GetPngAsync`, `GetPngBytesAsync` and `GetStrokesAsync` return the other forms on demand. The typed route is on by default because it is the accessible one; `AllowTyped="false"` removes it.
- **BbListBox**: an always-visible list of options with single (`@bind-Value`) or multiple (`@bind-Values`) selection, using the same `SelectOption<TValue>` as **BbSelect**. It has the full listbox keyboard pattern, `ShowSearch`, a `ShowSelectAll` that covers only the visible rows, `OptionDisabled`, `ItemTemplate` and `EditForm` validation.
- **BbPickList**: two **BbListBox** panes with move buttons between them. `Options` holds every option and `@bind-Values` holds the picked ones, in the order they were moved. The move-all buttons move only what the search leaves visible and skip disabled options, and `OnMove` reports what moved and which way.

### New Features

- **BbTabsList**: `Addable`, `Closable`, `Renamable` and `Reorderable`, each paired with a callback (`OnAdd`, `OnClose`, `OnRename`, `OnMove`). All are off by default, and a flag without its callback draws nothing. The tabs only ask, so you change the collection the tabs come from.
- **BbTabsTrigger**: `Closable`, `Renamable` and `Reorderable` override the list for one tab, for example to pin it. Delete or Backspace closes a tab, F2 or a double-click renames it, and setting `Cancel` on `TabRenameContext` reopens the editor with the typed text.

### Bug Fixes

- **BbDrawer**: `OpenChanged` now fires when `Open` is not bound. Before, a page that listened without binding `Open` heard nothing. It fires only on a real change, and a bound drawer is unchanged.

### Improvements

- **BbRichTextEditor**: Quill 2.0.3 now ships inside the package and loads on first use, so the host page no longer needs Quill `<script>` or `<link>` tags. A host that loads its own Quill first keeps it. The package grows by about 214 KB of static assets.
- **AsChild triggers**: **BbCollapsibleTrigger**, **BbPopoverTrigger**, **BbDialogTrigger**, **BbDialogClose**, **BbSheetTrigger**, **BbSheetClose**, **BbDropdownMenuTrigger**, **BbAlertDialogTrigger**, **BbAlertDialogAction** and **BbAlertDialogCancel** now log a Development-only warning when nothing inside them reads the trigger context. Before, text or an icon inside such a trigger did nothing, and nothing said why.
- **BbDrawerTrigger**, **BbDrawerClose**: with `AsChild="true"`, log the same Development-only warning when the child cannot read the trigger context, such as a plain `<button>`.
- **Localization**: `DefaultBbLocalizer` adds default strings for the new components and the new tab actions.
