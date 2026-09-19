## What's New in v4.0.0-beta.10

**This is a prerelease.** The API may still change before the stable v4.0.0 release.

### Breaking Changes

- **.NET 10**: the package now targets `net10.0` only and depends on `Microsoft.AspNetCore.Components.Web` 10.0.12. .NET 8 and .NET 9 are no longer supported.
- **BlazorBlueprint.Primitives**: the dependency is now 4.0.0-beta.10, which has its own breaking changes. Keep Components and Primitives on matching v4 versions. See the Primitives release notes and `V4-MIGRATION-GUIDE.md`.
- **BbPortalHost**: the portal hosts move from `BlazorBlueprint.Primitives.Services` to `BlazorBlueprint.Primitives`. Add `@using BlazorBlueprint.Primitives` to the layout that holds the host. Without it Razor emits a literal `<bbportalhost>` element, with no build error, and every overlay silently fails to render.
- **Stylesheet**: every Tailwind utility in `blazorblueprint.css` is now prefixed `bb:` (`.bb\:flex`) and lives in its own `bb-utilities` cascade layer, so your Tailwind build and the library's can no longer emit the same class. The layer order is `properties, theme, base, components, bb-utilities, utilities, bb`.
- **Class parameter**: no markup change is needed. `ClassNames.cn` merges across the prefix, so `Class="p-6"` still replaces the library's `bb:p-4`.
- **Tailwind `@source`**: remove any `@source` that points at the Blazor Blueprint package or sources. Under a prefixed build it emits nothing.
- **Projects without a Tailwind build**: bare utilities in your own markup (`class="flex gap-4"`) that relied on `blazorblueprint.css` now match nothing. Add a Tailwind build, or use the prefixed classes as a stopgap; that class set is not a stable API.
- **shimmer**, **scroll-fade-x**: renamed to `bb:shimmer` and `bb:scroll-fade-x`.
- **Theme variables**: Tailwind's generated variables are prefixed too (`--bb-spacing`, `--bb-default-transition-duration`). Semantic tokens such as `--background` and `--border` are unchanged.
- **CursorExtensions.ToClass**: returns the prefixed class (`bb:cursor-pointer`).
- **Internal class names**: CSS, JavaScript or tests that select internal elements by utility class (`.flex-col`, `.hidden`) need the prefix. Prefer the `data-slot` and other data attributes, which are stable.
- **BbCalendar**: `Mode` and the `CalendarMode` enum are removed. The parameter never selected anything; use **BbDateRangePicker** for a range.
- **BbCommand**: `CloseOnSelect` is removed. Nothing read it; close the surrounding overlay from `OnValueChange`.
- **BbCandlestick**, **BbFunnel**, **BbGauge**, **BbHeatmap**, **BbPie**, **BbRadar**: `Stacked` and `StackGroup` are removed. These series cannot stack, and the values were never read. Both stay on **BbBar**, **BbLine**, **BbArea**, **BbScatter** and **BbRadialBar**.
- **Menus**: **BbDropdownMenu**, **BbContextMenu** and **BbMenubar** content and items now take their colours from `--bb-menu-*` tokens. The default hover and focus highlight is a tint of the menu foreground, not `--accent`. Set `--bb-menu-accent: var(--accent)` and `--bb-menu-accent-foreground: var(--accent-foreground)` to restore the old highlight.
- **BbCarousel**: drag and swipe navigation is now on by default (`Draggable="true"`), and the root element is keyboard-focusable. JavaScript now positions the slides, so **BbCarouselContent** no longer renders an inline `transform`. `SlidesPerView` below 1, a negative `Gap` or an `AutoplayInterval` below 1000 now throws.
- **BbDataGrid**: a paged `IQueryable` source without search, grouping or virtualization now runs the full query only when a CSV export is requested. Keep its query provider (for example a `DbContext`) alive until then.
- **ThemeService**: `SetRadiusAsync` throws for values outside 0–4 rem, and invalid `ThemeOptions` defaults throw when the service is created. A stored radius outside that range is ignored.
- **BbTooltipTrigger**: `AsChild` now defaults to `false`. Add `AsChild="true"` where the child consumes the trigger context itself, such as a `BbButton`.
- **BbDrawerTrigger**, **BbDrawerClose**: now render a real `<button type="button">` and gain `AsChild`. Set `AsChild="true"` when the child is already a control, or you get a button inside a button.
- **BbResizableHandle**: each handle is now a keyboard-operable `role="separator"` and a tab stop, so the tab order of a resizable layout changes.
- **BbEventCalendar**: a multi-day event draws as one bar instead of one chip per day, so it is one tab stop per week row rather than one per day. A bar also spends the `MaxEventsPerDay` budget on every day it covers, so those days show fewer chips and count the difference into "+x more".
- **JavaScript modules**: components import their module once per circuit through `JsModules.GetAsync` / `PrimitiveModules.GetAsync` and no longer dispose it. Primitive modules are reached through `bb-primitives.js` under a namespace (`elementUtils.isNearBottom`). Custom code that imported individual primitive files must be updated.
- **Core bundle**: `theme.js`, `sidebar.js`, `sidebar-inset.js`, `text-input.js` and `composition-guard.js` ship as `bb-components-core.js` under a namespace (`theme.initialize`). Import it through `ComponentModules.GetCoreAsync`.
- **BbPopoverContent**, **BbSelectContent**, **BbDropdownMenuContent**: `BbFloatingPortal` wires dismissal and listbox keys in the call that opens the overlay. JavaScript owns `data-side`, `data-focused` and `aria-activedescendant`, so stop rendering them from C#.
- **BbSortable**: keyboard sorting is on by default, so each item, or its drag handle, is now a tab stop. Set `KeyboardSorting="false"` to keep the old tab order.
- **BbSortable**: in a drop between two connected lists, the source list's `OnRemove` now runs before the target list's `OnAdd`.
- **IVirtualizedGroupHandler**: gains `TryHoverItem(string elementId)`. Custom implementations must add it.
- **BbRichTextEditor**: Quill 2 is now required. The Quill 1 fallback for `getSemanticHTML` is removed, and the setup notes pin `quill@2.0.3`.
- **BbScheduler**: a single click on a slot or an event now only highlights it. Double-click, Enter or the context menu opens the editor. Enter on an empty slot still creates an event.

### New Components

- **BbScheduler**: day, week and work-week time slots with resource lanes, overlapping events, an event editor with delete confirmation, and drag-to-move and resize that snap to `SlotMinutes`. Supports recurrence (edit one occurrence or the series), IANA time zones with DST checks, `FirstDayOfWeek`, `InitialScrollHour`, and `OnEventChange` with `Cancel` to reject a change.
- **SchedulerEngine**: public helpers to expand recurring events (`Expand`), apply an edit (`ApplyChange`), validate an event and convert a local time to an instant (`ToInstant`).
- **BbTreeSelect**: searchable hierarchy picker with single or multiple selection, cascading checkboxes with indeterminate states, `LeafOnly`, clearing and `EditContext` binding.
- **BbCascader**: column-based hierarchy picker with full-path search, optional branch selection (`ChangeOnSelect`), keyboard and RTL navigation, and `EditContext` binding.
- **BbDateInput**, **BbTimeInput**: culture-aware segmented date and time entry with keyboard increments, min/max bounds, an optional calendar or time picker, and `EditContext` validation.
- **BbFormFieldDateInput**, **BbFormFieldTimeInput**, **BbFormFieldTreeSelect**, **BbFormFieldCascader**, **BbFormFieldQuantityStepper**: form field wrappers with label, helper text and validation message for the five new bindable controls. **BbFormFieldTreeSelect** reads `ValuesExpression` in `Multiple` mode and `ValueExpression` otherwise.
- **BbAppBar**: top app bar with title, description, back button, actions, sticky positioning and safe-area padding.
- **BbBottomNav**, **BbBottomNavItem**: bottom tab navigation with a bindable `Value`, links, icons, fixed positioning and safe-area padding.
- **BbNotificationBadge**: count or dot badge over any content, with `Maximum`, `ShowZero`, `Position` and `Variant`.
- **BbQuantityStepper**: integer stepper with `Min`, `Max` and `Step`, `EditContext` binding, and an `OnRemove` callback when decreasing at the minimum.
- **BbSectionHeader**: section title with description, actions and an optional separator.
- **BbMotion**: preset or custom keyframe animations, triggered on visibility, in view, hover, press or from code (`PlayAsync`). Respects reduced motion.
- **BbHeightAnimation**: animates expanding, collapsing and content resizing while keeping the content mounted.
- **BbSelectionIndicator**: an indicator that slides to the active element and can follow hover and keyboard focus.
- **BbPageTransition**, **BbScreenTransition**: animate incoming page content, or new screen content when `TransitionKey` changes.
- **BbRenderStateProvider**: cascades a `RenderState` that tells content when the app is interactive.
- **BbThemeScope**: applies a `ThemeDesign` and an optional radius to a subtree, including its floating overlays.
- **BbSidebarPillNav**, **BbSidebarPillNavItem**, **BbSidebarPillInset**: floating pill navigation for the collapsed `SidebarCollapsedMode.Pill` sidebar.
- **BbSidebarSelectionIndicator**: animated selection indicator for sidebar menus.
- **Menu submenus and radio items**: **BbDropdownMenu**, **BbContextMenu** and **BbMenubar** each gain `Sub`, `SubTrigger`, `SubContent`, `RadioGroup` and `RadioItem` components. **BbContextMenuCheckboxItem** is also new.
- **BbSortableHandle**: accessible drag handle with a default grip icon.
- **BbBadgeIcon**: small decorative icon for a **BbBadge**, by Lucide `Name` or custom content.

### New Features

- **BbScheduler**: `SchedulerView.Month` adds a month grid of six week rows, capped by `MaxEventsPerDay` and navigated a calendar month at a time. Drag and resize are off in that view.
- **BbScheduler**: all-day events through `SchedulerEvent.IsAllDay`, drawn as bars in a band above the time grid. `End` is exclusive, matching iCalendar's `DTEND`, and recurrence counts whole local days, so a daily series holds its date across a 23- or 25-hour day.
- **BbScheduler**: multi-day bars stack into lanes, a run crossing a week boundary is squared off at the join, `MaxAllDayRows` caps the band, and with two or more resources visible the band groups by resource.
- **BbScheduler**: context menus on slots and events, replaceable through `SlotContextMenuContent` and `EventContextMenuContent`. Render the context's `DefaultItems` to keep the built-in entries. Delete confirms without opening the editor. New `SchedulerSlotMenuContext` and `SchedulerEventMenuContext`.
- **BbScheduler**: a resource filter with bindable `VisibleResourceIds` and `ShowResourceFilter`. Null shows every resource, an empty list shows none, and the empty string selects the Unassigned lane.
- **BbScheduler**: `ActiveHours` mutes the slots outside each weekday's ranges, and `BlockOutsideActiveHours` turns muting into refusal. New `SchedulerDayHours`.
- **BbScheduler**: `ToolbarContent` and `EditorContent` wrap or replace the toolbar and the event editor. Both carry `DefaultContent`, through the new `SchedulerToolbarContext` and `SchedulerEditorContext`.
- **BbScheduler**: `TimeZones` supplies your own zone list and labels through `SchedulerTimeZone`, instead of the full IANA list named by identifier. A stored zone outside the list is appended, so an event is never moved quietly.
- **SchedulerEvent**: now derivable, so an application can carry its own fields. `Clone` is virtual, `CopyTo` is protected, and **BbScheduler**'s `NewEventFactory` makes a new event your type.
- **SchedulerEngine**: new `StartOfDay`, which resolves a local date to an instant in zones that advance the clock at midnight.
- **BbTagInput**: `TagInputTrigger.Blur` commits the typed text when the input loses focus. It is off by default, it commits the text rather than a highlighted suggestion, and rejected text stays in the input and reports through `OnTagRejected`.
- **BbDataGrid**: `DataGridEditMode.Cell` and `Batch` editing. Drafts are isolated copies from `EditItemFactory` (required for these modes), validated before save, and kept when a save is rejected.
- **BbDataGrid**: batch editing adds `OnBatchCommit`, `OnBatchCancel`, `CommitBatchAsync` and `CancelBatchAsync`, and `StartCellEditAsync` opens a cell editor from code.
- **DataGridRowCommitContext**: new `OriginalItem`, the unchanged source record in cell mode. **DataGridBatchCommitContext** is new.
- **BbFileUpload**: optional `UploadHandler` transport with progress, cancellation and retry. Adds `AutoUpload`, `OnUploadFinished`, `UploadFilesAsync`, `UploadFileAsync` and `CancelUpload`.
- **FileUploadItem**: new `Status`, `BytesTransferred`, `Progress` and `UploadError`. **FileUploadContext** reports progress through `ReportProgressAsync`.
- **BbDataView**: selection (`SelectionMode`, `SelectedItems`, `ItemKey`, `IsItemDisabled`), grouping (`GroupBy`, `GroupHeaderTemplate`) and list virtualization (`EnableVirtualization`).
- **BbDataView**: `MobileToolbar` moves sorting and the new `FilterContent` into a bottom sheet.
- **BbDataView**: new `SearchDebounceMs`, matching **BbDataGrid**.
- **BbEventCalendar**: a multi-day event draws as one bar across the days it covers, in the month view and the week view. Bars are packed into lanes so they never overlap, an event crossing a week boundary becomes one bar per row, and both halves carry the whole event's dates in their `aria-label`. `EventClass` and `EventTemplate` apply to bars as they did to chips, and the agenda view is unchanged.
- **BbEventCalendar**: new `ContainerClass`, which targets the view container in all three views, so the calendar can be sized without also sizing the toolbar.
- **BbResizableHandle**: keyboard resizing. Arrows move the handle 5%, Page Up/Down 20%, and Home/End take the panel to its limits. Adds `AriaLabel`.
- **BbCascader**: new `Required`, matching **BbTreeSelect**.
- **BbCascader**, **BbTreeSelect**, **BbQuantityStepper**: new `AriaDescribedBy`, so a wrapper can point the control at its own error text.
- **BbDatePickerInput**: captures unmatched attributes.
- **BbSelect**: `Presentation="SelectPresentation.BottomSheet"` shows the options in a modal bottom sheet, titled by `SheetTitle`.
- **BbMultiSelect**: `FooterContent` replaces the default footer, and `CloseAsync` closes the list from code.
- **BbFilterBuilder**: saved `Presets` shown as buttons or a dropdown (`PresetDisplay`, `ApplyPresetAsync`), `SearchableFields`, and per-field `ValueEditors` templates.
- **BbCarousel**: autoplay with a pause/play button, multiple or fractional `SlidesPerView`, `Gap`, built-in indicators, bindable `ActiveIndex`, `OnSlideChanged` and `GoToAsync`.
- **BbDrawer**: `SnapPoints` with a bindable `SnapIndex`, pointer and arrow-key resizing, and `DismissOnDrag`.
- **BbSortable**: keyboard sorting (pick up, move, move to a connected list with Control+Left/Right, drop, cancel) with `KeyboardSorting` and `KeyboardInstructions`, `CanMove` and `CanDrop` rules, and a `DragOverlayTemplate` preview.
- **BbSidebarProvider**: new `CollapsedMode`. `Pill` replaces the icon rail with floating pill navigation when the sidebar collapses.
- **Theme presets**: `ThemeDesign` sets density, font stack, card and menu surfaces, and menu colours, and is saved with the theme. Fonts are not downloaded; your app supplies them.
- **ThemeService**: new `SetPresetAsync`, `SetDesignAsync` and `Preset`. **ThemeOptions** gains `DefaultPreset`, and `ThemePresets` offers seven starting points.
- **BbThemeSwitcher**: `ShowDesignOptions` shows the design settings.
- **BbBadge**: new `Success`, `Warning` and `Info` variants, plus soft variants (`Soft`, `SoftDestructive`, `SoftSuccess`, `SoftWarning`, `SoftInfo`).
- **BbToggleGroup**: `Required` keeps the last selection, and `Scrollable` scrolls the items horizontally.
- **BbSeparator**: `LineStyle` for solid, dashed or dotted lines.
- **BbRichTextEditor**: tables through Quill 2's built-in table module. The `Full` toolbar gains a table button, and the component gains `InsertTableAsync`, `InsertRowAboveAsync`, `InsertRowBelowAsync`, `InsertColumnLeftAsync`, `InsertColumnRightAsync`, `DeleteRowAsync`, `DeleteColumnAsync` and `DeleteTableAsync`.
- **BbRichTextEditor**: the `Standard` toolbar gains undo, redo and a checklist. `UndoAsync` and `RedoAsync` are new, and `TextChangeEventArgs` reports `CanUndo` and `CanRedo`.
- **BbRichTextEditor**: the `Full` toolbar gains inline code, alignment, text colour, highlight and images. Alignment and colours are written as inline styles.
- **BbRichTextEditor**: new `ImageUploader` and `MaxImageSize` (10 MB default) to store picked, dropped or pasted images, plus `InsertImageAsync` and the `EditorImageUpload` type. Without an uploader, images embed as data URLs.
- **BbDialog**: new `RenderingStrategy`. `OverlayRenderingStrategy.Native` renders a browser `<dialog>` that needs no portal host and works across render-mode boundaries.
- **BbDialogContent**: in native mode, `CloseOnOverlayClick` controls backdrop clicks, and the stylesheet styles the native backdrop.
- **BbPopoverContent**: new `ScrollToSelected`, `ScrollToSelectedSelector` and `AutoFocusId`, applied in the call that positions the popover.
- **BbCommandInput**: new `Id`, so an owner can target the search box for focus.
- **BbCopyText**: new `ValueFuncAsync` for text that must be fetched, copied inside the user gesture so the write survives a slow callback.
- **BbCopyText**: new `OnCopyFailed` callback with a `CopyTextFailure` of `Refused` or `NoValue`.
- **ComponentModules**: new static helper with `CorePath`, `CoreUrl`, `GetCoreAsync` and `TryGetCoreLoaded` for the core JavaScript bundle.

### Bug Fixes

- **BbDataGrid**: a cell editor no longer paints over Save and Cancel in a narrow column, and a checkbox, switch or toggle editor keeps its border instead of stretching to an empty-looking cell.
- **BbScheduler**: a stray `}` no longer renders as text in the event editor.
- **BbScheduler**: the toolbar heading no longer jumps above the navigation on a narrow container, the time-zone label no longer reads as part of the view buttons, and Month view with a resource selected no longer claims no resources are selected.
- **BbCalendar**: the day grid is centred, so the dates no longer hang to the left of a wider header. **BbDatePicker**, **BbDateRangePicker** and **BbDateTimePicker** embed the same calendar and pick the fix up.
- **BbContextMenuContent**: a menu opened near the right or bottom edge of the viewport now flips or clamps instead of opening off-screen, through the Primitives update.
- **Borders**: borders no longer render near-black under a consumer Tailwind build. The consumer's own preflight reset the shared `*` rule to `currentColor`, and which stylesheet won depended on link order. The default now matches on the class attribute instead.
- **group and peer markers**: the library shipped only the prefixed `bb:group` and `bb:peer` markers, which a consumer's Tailwind build never matches, so every `group-*` and `peer-*` variant written against library markup did nothing. `ClassNames.cn` now carries the bare twin of any marker it keeps, which covers 30 components.
- **Overlay roots**: an extra HTML attribute on **BbDialog**, **BbSheet**, **BbPopover**, **BbHoverCard** and the other overlay roots crashed the render with `InvalidOperationException`. The context-only roots now accept extra attributes, and the roots that render an element put them on it.
- **ARIA state**: attributes bound to a `bool` rendered an empty value when true and vanished when false.
- **Parameters that did nothing**: `BbSelect.Open`, `BbCommand.Disabled`, `BbRangeSlider.Orientation`, `BbResizablePanel.Collapsible`, `BbToastProvider.MaxToasts`, `BbRadialBarChart.EndAngle`, `BbSidebarMenuAction.ShowOnHover` and others now work.
- **BbSlider**, **BbRangeSlider**: a vertical slider now lays out vertically. **BbSlider** read `Orientation` nowhere, and the track's `grow` followed the flex main axis. The range slider's value tooltips and tick marks move to opposite sides so they cannot overlap.
- **BbDataGrid**: the global search ignored `SearchDebounceMs`, because the input still reported on blur or Enter.
- **BbPortalHost**: a live host reported itself missing. Two hosts overlap more often than a boolean allowed, so registration is a clamped count now.
- **BbRadioGroup**: Tab can leave the group again. It suppressed the default action of every key.
- **BbSwitch**: Space toggles once, not twice.
- **BbToggleGroup**: single mode no longer announces as a radio with no state, and it unregisters its items.
- **BbNativeSelect**: enum values bind. **InputConverter** parses enums, which `Convert.ChangeType` cannot produce.
- **Inline styles**: **BbAspectRatio**, **BbScrollArea**, **BbDashboardGrid**, **BbResizablePanel**, **BbResizablePanelGroup**, **BbSkeleton**, **BbChartBase**, **BbDropdownMenuContent** and the **BbCommand** groups merge a consumer `style` with their own. Any style at all used to replace flex sizing, grid templates or the `display:none` that hides a closed menu.
- **BbInput**, **BbInputField**, **BbTextarea**, **BbInputGroupInput**, **BbInputGroupTextarea**: `UpdateTiming` and `DebounceInterval` changes after the first render now reach the browser. They were fixed at their first-render values.
- **BbNumericInput**, **BbCurrencyInput**: a step at `int.MaxValue` no longer overflows to a large negative number. The value pins to the configured limit.
- **Charts**: per-series colours apply, and **BbCandlestick** click arguments report the right values.
- **BbDataGrid**: grouped virtualized rows render correctly.
- **BbNavigationMenu**: trigger registration is keyed by the trigger, so a trigger that leaves the page takes its entry with it.
- **BbDialog**, **BbSheet**: the primitive roots declare `IDisposable`, so the `Dispose` they already had is called. The native `<dialog>` fallback and the overlay registration order are fixed too.
- **BbDataGrid**: initial sorting now applies to the first rows, so they match the sort indicators.
- **BbDataView**: with `ShowPagination="false"`, local data is no longer cut to the first page.
- **BbDataView**: a parent re-render no longer undoes a layout the user toggled, and infinite scroll keeps working after a provider load replaces the scroll container.
- **BbDrawer**: closing returns focus to the trigger that opened it, including composed triggers.
- **BbDatePicker**: focus returns to the trigger after a date is picked.
- **BbMultiSelect**: Escape closes the list and stops there, so it no longer reaches an enclosing overlay.
- **BbTreeView**: in checkable, non-strict mode, checking a parent now reaches children hidden by the search filter.
- **BbColorPicker**: dragging does nothing while `Disabled` is set.
- **Core bundle**: the modules inside `bb-components-core.js` now load from revised URLs, so a cached older `sidebar.js` cannot break an upgraded app.
- **Core bundle**: a stale cached module now fails at load with an error that names the file, instead of killing the circuit.
- **BbSidebarInset**: client-side navigation no longer kills the circuit with `Could not find 'scrollToTop'` (a regression in 4.0.0-beta.1).
- **BbCommand**, **BbCommandInput**, **BbCommandVirtualizedGroup**, **BbNavigationMenu**, **BbResponsiveNavProvider**, **BbSidebarProvider**, **BbSidebarInset**: fire-and-forget handlers now catch all exceptions, so none can end a Blazor Server circuit.
- **BbCombobox**: closing no longer fires `SearchQueryChanged` with an empty string when nothing was typed, so an infinite-scroll list is not reloaded.
- **BbCombobox**: reopens scrolled to the chosen item, which is marked with `data-bb-current`.
- **BbCopyText**: the clipboard write happens inside the user gesture, so Safari accepts it. Enter and Space are handled in JavaScript.
- **BbCopyText**: the `execCommand` fallback runs only in insecure contexts, so it no longer reports success with an empty clipboard.
- **BbDrawerTrigger**, **BbDrawerClose**: now show the themed focus ring.
- **BbPopoverContent**, **BbDropdownMenuContent**: no longer render twice on open.
- **BbDialog**, **BbAlertDialog**, **BbSheet**, **BbDrawer**: Tab no longer escapes the modal when focus is on the container or on a listbox outside the tab order, which WebKit allowed.
- **BbDropdownMenu**, **BbContextMenu**, **BbMenubar**: keys pressed with Ctrl, Alt or Meta are ignored, and in a right-to-left **BbMenubar** Left and Right move to the correct menu.
- **BbToggleGroup**: without a bound value, items show the pressed state as soon as they are toggled.
- **BbSortable**: no longer calls `OnUpdate` for an out-of-range or unchanged index, or when `Sort` is false.

### Improvements

- **Scrollbars**: native scrollbars inside library components follow the theme in light and dark mode, and the stylesheet sets `color-scheme` for each mode.
- **BbSidebar**: a closed non-collapsible sidebar is now `inert` and `aria-hidden`, and its width transition respects reduced motion.
- **Localization**: `DefaultBbLocalizer` adds strings for the new components and features, including the resizable handle, and the last of the hard-coded English is gone.
- **Localization**: `DefaultBbLocalizer` adds strings for the scheduler's month view, all-day band, context menus, resource filter and active hours.
- **Package**: adds a dependency on `Ical.Net` 5.2.3 for scheduler recurrence. The package now includes `LICENSE`, `NOTICE` and `THIRD-PARTY-NOTICES.txt`, also served at `_content/BlazorBlueprint.Components/THIRD-PARTY-NOTICES.txt`.
- **BbRichTextEditor**: `table`, `code`, `align`, `color`, `background` and `image` are registered formats, so bound or pasted HTML keeps them. The sanitizer allows `data:image/*` on `<img src>` only.
- **BbDarkModeToggle**: icons are now `h-4 w-4`, matching **BbThemeSwitcher**.
- **Reduced motion**: the `.bb-no-animate` exemption matches both `bb:animate-spin` / `bb:animate-pulse` and bare `animate-spin` / `animate-pulse`.
- **BbCommandInput**: the focus ring moves from the `<input>` to its row.
- **ThemeService**: invalid colour names in `localStorage` fall back to the default in the browser, without an extra round trip.
- **BbNavigationMenuTrigger**: ArrowDown no longer waits 50 ms before focusing the first item.
- **BbSortable**: items render with `role="listitem"`, and the live status region and keyboard instructions have stable ids.
- **Documentation**: `V4-MIGRATION-GUIDE.md` is now the single list of v4 breaking changes, and the CHANGELOG links into it.

### Performance

- **BbDataGrid**, **BbDataView**: the search box debounces in the browser, which costs one provider call per typing pause rather than one per key. This matters most on Blazor Server.
- **BbDataGrid**: paged `IQueryable` sources without search, grouping or virtualization count and page on the query provider instead of loading every row.
- **BbCommand**: filtered results and item positions are cached and shared, so items no longer rescan the filtered list.
- **BbSlider**, **BbRangeSlider**, **BbColorPicker**: drag feedback updates in the browser. Value updates during a drag are sent at most about every 50 ms, and the final value is sent on release.
- **BbRating**: hover updates when the pointer enters an icon, not on every mouse move.
- **BbTreeView**: search indexes parents and visible nodes, so rendering no longer repeats descendant searches.
- **BbEventCalendar**: a multi-day bar is positioned with a `calc()` over the seven-column grid, so it needs no measuring and no JavaScript at any width.
- **Overlays**: **BbSelect**, **BbPopover**, **BbDropdownMenu**, **BbCombobox** and other floating overlays open and close in one interop call each instead of five, through the Primitives update.
- **BbCombobox**, **BbMultiSelect**: the search box is focused inside the call that opens the popover, not after a render, a 50 ms wait and another round trip.
- **BbSelect**, **BbPopover**, **BbDropdownMenu**: focus returns to the trigger inside the close call.
- **BbPopoverContent**: requests the portal ready callback only when `OnContentReady` is set.
- **BbCommandItem**: one delegated hover listener per list replaces per-item mouse handlers, and `ShouldRender` stops a focus move from re-rendering every item.
- **BbCommandList**, **BbSelectContent**, **BbMultiSelect**, **BbDataView**: infinite scroll watches for the bottom in the browser and calls .NET once, instead of a round trip per scroll event.
- **JavaScript modules**: each module is imported once per circuit and shared by all component instances.
- **Core bundle**: the five modules used on most pages ship as one file, cutting module imports per page from 3–6 to 1–3.
- **ThemeService**, **BbSidebarProvider**: initialize in one interop call each instead of two to four.
- **BbDataGrid**: key and click handlers are attached once to the grid and delegated, instead of once per row.
