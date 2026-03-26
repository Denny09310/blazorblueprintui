## What's New in v3.8.1

### New Components

- **ThemeService** — scoped service for managing dark mode, base color, primary color, and border radius with `localStorage` persistence and OS preference detection
- **BbThemeSwitcher** — interactive theme customization panel for switching primary color, base color, radius, and light/dark mode
- **BbDarkModeToggle** — button component for toggling between light and dark mode

### New Features

- **DataGrid** global search — new `ShowSearch` parameter renders a built-in debounced search input; filters across all `Filterable` columns client-side or passes `SearchText` to `ItemsProvider` via `DataGridRequest.SearchText` for server-side filtering
- **DataGrid** virtualized server-side scrolling — when both `Virtualize` and `ItemsProvider` are set, the grid uses Blazor's `Virtualize` component to stream rows on scroll instead of loading all data at once
- **DataGrid** `Striped` and `StripeClass` parameters for alternating row backgrounds
- **DataGrid** `OverscanCount` parameter to control how many extra rows are rendered outside the visible area during virtual scrolling
- **DataGrid** `VirtualScrollHeight` parameter to set the scroll container height in virtualized server-side mode
- **DataGrid** `TableContainerClass` parameter for styling the inner scrollable container
- **DataView** `GridColumnMinWidth` parameter — uses CSS `repeat(auto-fill, minmax(...))` for fluid grid layouts instead of fixed breakpoint columns
- **BbChartTooltip** `AppendToBody` parameter to render chart tooltips outside the chart container, preventing clipping by `overflow: hidden` parents
- **Theme system** CSS with OKLCH color definitions for 5 base color palettes (Zinc, Slate, Gray, Neutral, Stone) and 18 primary accent colors

### Improvements

- **Menubar** components now delegate to Primitives-layer counterparts for keyboard navigation, focus management, and ARIA semantics, significantly reducing duplicated logic
- **DataGrid** pagination controls are now responsive — page size selector, page display, and first/last buttons hide on smaller screens
- **Localization** keys added for DataGrid search placeholder and all Theme components
- **ThemeService** registered via `AddBlazorBlueprintComponents()` with optional `Action<ThemeOptions>` configuration
- Bumped **BlazorBlueprint.Primitives** dependency to 3.8.0
