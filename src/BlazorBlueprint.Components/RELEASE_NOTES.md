## What's New in v4.0.0

### Breaking Changes

- **.NET 10**: the package now targets `net10.0` only and depends on `Microsoft.AspNetCore.Components.Web` 10.0.12. .NET 8 and .NET 9 are no longer supported.
- **BlazorBlueprint.Primitives**: the dependency is now 4.0.0, which has its own breaking changes. Keep Components and Primitives on matching v4 versions. See the Primitives release notes and `V4-MIGRATION-GUIDE.md`.
- **BbPortalHost**: the portal hosts move from `BlazorBlueprint.Primitives.Services` to `BlazorBlueprint.Primitives`. Add `@using BlazorBlueprint.Primitives` to the layout that holds the host. Without it Razor emits a literal `<bbportalhost>` element, with no build error, and every overlay silently fails to render.
- **Stylesheet**: every Tailwind utility in `blazorblueprint.css` is now prefixed `bb:` (`.bb\:flex`) and lives in its own `bb-utilities` cascade layer, so your Tailwind build and the library's can no longer emit the same class. The layer order is `properties, theme, base, components, bb-utilities, utilities, bb`.
- **Class parameter**: no markup change is needed. `ClassNames.cn` merges across the prefix, so `Class="p-6"` still replaces the library's `bb:p-4`.
- **Tailwind `@source`**: remove any `@source` that points at the Blazor Blueprint package or sources. Under a prefixed build it emits nothing.
- **Projects without a Tailwind build**: bare utilities in your own markup (`class="flex gap-4"`) that relied on `blazorblueprint.css` now match nothing. Add a Tailwind build, or use the prefixed classes as a stopgap; that class set is not a stable API.
- **shimmer**, **scroll-fade-x**: renamed to `bb:shimmer` and `bb:scroll-fade-x`.
- **Theme variables**: Tailwind's generated variables are prefixed too (`--bb-spacing`, `--bb-default-transition-duration`). Semantic tokens such as `--background` and `--border` are unchanged.
- **CursorExtensions.ToClass**: returns the prefixed class (`bb:cursor-pointer`).
- **Internal class names**: CSS, JavaScript or tests that select internal elements by utility class (`.flex-col`, `.hidden`) need the prefix. Prefer the `data-slot` and other data attributes, which are stable.
- **Logical CSS utilities**: every spacing, border, radius and alignment utility the library emits is now logical (`ms-2` rather than `ml-2`, `pe-1` rather than `pr-1`) across 110 files. CSS or tests that matched a physical utility class on library markup need updating. `Class` overrides are unaffected.
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

- **BbChip**, **BbChipSet<TValue>**: an interactive badge with a selected state, a dismiss button, or both, in five variants and three sizes. A chip renders as a plain span until something makes it interactive; the set owns `None`, `Single` or `Multiple` selection, supplies the chips' defaults, honours `Required`, and drops a dismissed chip's value from the selection before reporting it.
- **BbFab**: a floating action button, raised and pinned to one of five logical placements, fixed to the viewport or to the nearest positioned ancestor. Built on **BbButton**, so it works as an `AsChild` trigger for a speed dial. Sits above a bottom navigation bar, clears the device safe area, and `Shape="FabShape.Circle"` makes it a circle, or a pill once it carries a label.
- **BbStepper**, **BbStep**: a progress indicator for a sequence of steps, horizontal or vertical, with optional per-step content rendered only while that step is active. State is derived from position and `BbStep.State` overrides it. Deliberately not a form, which is what separates it from **BbFormWizard**.
- **BbLink**: the inline counterpart to **BbButton**, rendering a bare anchor on the text baseline. Four colour treatments including one that inherits the surrounding text, `Underline` always / on hover / never, and a focus ring that follows the text so a link wrapping mid-sentence reads as one link. `Target="_blank"` adds `rel="noopener noreferrer"`, and `ShowExternalIcon` appends an icon with a screen-reader note.
- **BbHighlighter**: marks the runs of a string that match one or many search terms, as `<mark>` elements so the highlight is not colour alone. Overlapping matches merge into one run, `WholeWord` stops a short term marking a fragment, and the text renders as text, so a term from a search box cannot inject an element.
- **BbImage**: shows your own content, a second URL through `FallbackSrc`, or a neutral placeholder when a source fails. The fallback keeps the accessible name and inherits your classes, `OnError` fires as well, and images are lazy by default.
- **BbScrollToTop**: a floating button that appears once the document, or a panel named by `Selector`, is scrolled past `VisibleAt`, and returns it to the top. It renders nothing below the threshold, so it adds no tab stop on a short page. Built on **BbFab** and honours reduced motion.
- **BbExitPrompt**: holds a navigation while there is unsaved work. In-application navigation is refused and the component's own **BbAlertDialog** asks; closing the tab or reloading arms the browser's own prompt.
- **BbRoseChart**, **BbRose**: a pie whose sectors vary in radius as well as angle. `RoseMode.Radius` keeps proportional angles and adds radius on top; `RoseMode.Area` gives every sector the same angle and varies the radius alone. Derives from **BbPie**, so the donut hole, labels, leader lines and a child **BbCenterLabel** work unchanged.
- **BbSankeyChart**, **BbSankey**: shows how a quantity splits and recombines between stages. Binds a collection of links — a source name, a target name and a value per row — and derives the nodes from the names in first-seen order. Hovering a node dims everything it is not connected to. A link that would close a cycle, a row missing a name and a non-numeric value are dropped, so one bad row cannot blank the chart.
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

- **Right-to-left support**: wrap the layout in **BbDirectionProvider** and the library mirrors for Arabic or Hebrew. Mirroring is CSS, not C#, so there is no second stylesheet and no runtime branching. Overlays copy the direction from the element that opened them, so the provider works even when **BbPortalHost** sits outside it. `TextDirection.Auto`, the default, follows `CultureInfo.CurrentCulture`, so nothing changes until an application asks for it.
- **Right-to-left geometry**: the components that place content with pixel or percentage maths read the direction at the moment of the gesture, so the maths agrees with the paint. **BbSlider** and **BbRangeSlider** fill from the reading edge and swap their horizontal arrows, **BbCarousel** moves its arrows to the leading and trailing edges, **BbScheduler** and **BbEventCalendar** offset events past the gutter on the reading side, and **BbDashboardGrid**, **BbDock** and **BbResizable** drag, drop and resize along the reading direction.
- **Right-to-left keyboard navigation**: arrow keys follow the reading direction in tabs, toggle groups, radio groups, menus and the tree, while Up and Down keep their meaning. In a right-to-left tree, ArrowLeft opens a node.
- **Physical-side parameters keep their promise** and do not mirror: `SheetSide`, `DrawerDirection`, `SidebarSide`, `ToastPosition`, `BadgeDotPosition`, the `PopoverSide` an overlay reports, and `SankeyNodeAlign`. Pass the other value for the other side. See the Right-to-Left guide.
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

- **BbRadialBarChart**: a `Title` and a **BbCenterLabel** no longer evict each other. A radial bar borrows the chart title to draw the text in the hole, so setting both silently kept whichever was written last. Both now fit.
- **BbRadialBarChart**: the centre label is centred on the donut. It sat half its own size up and to the left, because the explicit `textAlign` made ECharts skip the shift that compensates for the anchor.
- **BbRadialBar**: with `ShowLabels`, bars carrying similar values no longer pile their labels into the same wedge. Each label sits at the start of its own ring, so the names stack instead of colliding.
- **BbSwitch**: the thumb travels the correct way in a right-to-left layout. A transform has no logical form, so the checked offset stayed physical and moved the thumb out of the track.
- **Stylesheet**: five hand-authored rules now use logical properties — the rich text editor's list indent and blockquote rule, the timeline's end alignment and padding, and the date-range picker's presets divider. They did not mirror because the logical-property sweep read `.razor` and `.cs` files, not stylesheets.
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

### Improvements and performance

NuGet caps release notes at 35,000 characters, so the remaining improvement and
performance entries live in the full changelog:
https://github.com/blazorblueprintui/ui/blob/main/CHANGELOG.md
