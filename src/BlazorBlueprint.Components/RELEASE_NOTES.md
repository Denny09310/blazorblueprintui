## What's New in v4.1.0 (unreleased)

> **Prerelease preparation:** these cumulative notes describe the current v4.1.0 branch. The release script selects the package version.

### Breaking Changes

- **BbDateTimePicker**: `MinuteStep` must be between 1 and 59; invalid values now throw during parameter validation.
- **BbDockPanel**: panel IDs must be nonempty and unique within their dock layout; invalid registrations now throw.

- **BlazorBlueprint.Primitives**: the audit fixes require the matching Primitives build. Keep Components and Primitives on matching release versions, and see the Primitives release notes.
- **BbTabsTrigger**: Ctrl or Cmd with an arrow key, Home or End no longer moves focus to another tab. Ctrl or Cmd with an arrow key now moves the tab, and only when **BbTabsList** has `Reorderable` on and `OnMove` set. Otherwise it does nothing.
- **BbTooltipTrigger**, **BbHoverCardTrigger**: the Development-only AsChild warning now logs under the event name `TriggerContextUnconsumed` instead of `TooltipTriggerContextUnconsumed` and `HoverCardTriggerContextUnconsumed`, and its text changes. Update any log filter that matched the old names.
- **BbDataGrid**: the column resize module `js/datagrid-columns.js` is renamed `js/table-columns.js`, and each `<col>` now carries `data-column-id`. Only custom code that imported the old file directly needs to change.

### New Components

- **BbMapChart**, **BbMap**: a world choropleth chart with ISO country-code or English-name binding, automatic value-based coloring, `BbVisualMap` palette/range customization, no-data styling, tooltips, country clicks, and optional pan/zoom. World boundaries are bundled and loaded on demand; no API key is required.

- **BbGantt**, **BbGanttColumn**: a plan drawn against a timeline, with a task list, a bar per task and dependency arrows. It has six zoom levels, summary tasks that roll up dates and progress from their children, milestones, non-working-day shading and a today line. The chart draws dependencies but does not enforce them, so moving a task does not move the tasks after it.
- **BbGantt** editing: `AllowDrag`, `AllowResize`, `AllowProgressDrag`, `AllowLinking` and `AllowRowDrag` hand each change back through `OnTaskChange`, `OnDependencyCreate` and `OnTaskMove` rather than writing it, and each change can be refused. `ShowLegend`, `ShowTooltip`, sortable and resizable columns, and right-to-left pages are supported.
- **BbPivotDataGrid**, **BbPivotField**, **BbPivotValue**: row fields down the side, column fields across the top, and an aggregate where they cross. Totals and subtotals are worked out from every item under them, so an average total is a true average. `OnCellClick` gives the items behind a cell, `ShowFieldPicker` turns fields on and off, and paging never splits a row group.
- **BbQrCode**: a QR code drawn as SVG in the markup, with no JavaScript and no image request. It supports four error correction levels, `ModuleShape`, a centre `Image`, `ShowValue` and `AriaLabel`. The colours stay dark on light whatever the theme, and a value that is too long shows a message instead of throwing.
- **BbBarcode**: 14 barcode types drawn as SVG, including Code 128, Code 39, EAN-13, UPC-A, ISBN and POSTNET. Each type checks its characters, length and check digit, and an invalid value shows a message naming the problem instead of throwing. `ShowValue` prints the value as selectable text.
- **BbSignature**: sign by drawing or by typing a name. `@bind-Value` gives a `SignatureValue` with `Kind`, `Svg` and `Text`, and `GetPngAsync`, `GetPngBytesAsync` and `GetStrokesAsync` return the other forms on demand. The typed route is on by default because it is the accessible one; `AllowTyped="false"` removes it.
- **BbListBox**: an always-visible list of options with single (`@bind-Value`) or multiple (`@bind-Values`) selection, using the same `SelectOption<TValue>` as **BbSelect**. It has the full listbox keyboard pattern, `ShowSearch`, a `ShowSelectAll` that covers only the visible rows, `OptionDisabled`, `ItemTemplate` and `EditForm` validation.
- **BbPickList**: two **BbListBox** panes with move buttons between them. `Options` holds every option and `@bind-Values` holds the picked ones, in the order they were moved. The move-all buttons move only what the search leaves visible and skip disabled options, and `OnMove` reports what moved and which way.

### New Features

- **Accessible labels**: date/time pickers, selection controls, OTP input and file upload now expose `AriaLabel` on their interactive element; form wrappers forward it.
- **BbSectionHeader**: `HeadingLevel` selects h1 through h6, with h2 as the default.
- **Form wrappers**: checkbox groups, date ranges and file uploads support EditForm field expressions and field-change notifications. **BbMultiSelect** now has explicit `Required` and `ActiveClass` forwarding.

- **BbTabsList**: `Addable`, `Closable`, `Renamable` and `Reorderable`, each paired with a callback (`OnAdd`, `OnClose`, `OnRename`, `OnMove`). All are off by default, and a flag without its callback draws nothing. The tabs only ask, so you change the collection the tabs come from.
- **BbTabsTrigger**: `Closable`, `Renamable` and `Reorderable` override the list for one tab, for example to pin it. Delete or Backspace closes a tab, F2 or a double-click renames it, and setting `Cancel` on `TabRenameContext` reopens the editor with the typed text.

### Bug Fixes

- **BbDateRangePicker**: the mobile preset dropdown follows the selected calendar dates, including parent updates and desktop preset clicks. Unmatched/partial ranges show Custom, and clearing shows Select date range, so Today can always be selected when it is not the current range.
- **Radar tooltips**: data-derived indicator names, series names and values are HTML-escaped to prevent HTML injection.
- **BbSignature**: stroke restoration waits for the drawn pad to mount and initialize, including repeated restoration from typed mode.
- **BbPivotDataGrid**, **BbGantt**: changed parent headings and non-working-day bands redraw immediately. Gantt also renders initial build errors without requiring another parent interaction.
- **BbListBox**: the visible label supplies the accessible name, and navigation keys no longer consume the next Tab.
- **Charts**: click callbacks can be added, replaced and removed after initialization without remounting or changing chart data.
- **BbFileUpload**: explicitly setting bound `Files` to null clears the selection and releases removed resources; omitting `Files` still permits uncontrolled selection.

- **BbGantt**, **BbPivotDataGrid**: `OnBuilt` no longer loops when a parent handles it. Gantt waits for its rendered element before wiring JavaScript and preserves pending setup across deferred renders. **BbBarcode** displays encoder messages without framework resource keys on WebAssembly.
- **Tabs and steppers**: responsive tab lists support adding/reordering, and conditional steps follow their current markup order.
- **Dates and time**: blocked dates also apply to Now and empty-value time stepping; the first segment increment starts at its minimum; custom calendar day names update with parameters. Editable date inputs reflect EditForm validation state.
- **Selection and uploads**: required chip sets retain their last selected chip; toggle navigation accounts for changed disabled items; file-upload paste handling follows runtime `AllowPaste` changes.
- **ScrollToTop**: changed options and late/replaced targets are observed, focus is restored appropriately, and completion fires after reaching the top. **Motion** visibility changes only activate the Visibility trigger.
- **Rendering and callbacks**: pagination templates receive current state/options; signature empty-state reporting reflects restored strokes; message alignment and tinted bubble contrast are corrected.
- **Accessibility**: attachment actions have accessible names, decorative image fallbacks are hidden from assistive technology, and links announce a new tab only for `Target="_blank"`.
- **Chart colors**: explicit heatmap/candlestick series colors and map fill children are honored, with documented visual-map precedence.

- **BbDrawer**: `OpenChanged` now fires when `Open` is not bound. Before, a page that listened without binding `Open` heard nothing. It fires only on a real change, and a bound drawer is unchanged.

### Improvements

- **Localization**: sortable instructions and announcements, file-upload text/errors, AM/PM labels, and MultiSelect count/removal labels use `IBbLocalizer`.
- **Compatibility**: WholeWord retains word-start matching, Sidebar retains its controlled-mode callback requirement, and compact form-wrapper popup defaults remain unchanged.

- **BbRichTextEditor**: Quill 2.0.3 now ships inside the package and loads on first use, so the host page no longer needs Quill `<script>` or `<link>` tags. A host that loads its own Quill first keeps it. The package grows by about 214 KB of static assets.
- **AsChild triggers**: **BbCollapsibleTrigger**, **BbPopoverTrigger**, **BbDialogTrigger**, **BbDialogClose**, **BbSheetTrigger**, **BbSheetClose**, **BbDropdownMenuTrigger**, **BbAlertDialogTrigger**, **BbAlertDialogAction** and **BbAlertDialogCancel** now log a Development-only warning when nothing inside them reads the trigger context. Before, text or an icon inside such a trigger did nothing, and nothing said why.
- **BbDrawerTrigger**, **BbDrawerClose**: with `AsChild="true"`, log the same Development-only warning when the child cannot read the trigger context, such as a plain `<button>`.
- **Localization**: `DefaultBbLocalizer` adds default strings for the new components and the new tab actions.
