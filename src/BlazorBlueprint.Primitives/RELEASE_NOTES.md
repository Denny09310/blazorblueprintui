## What's New in v3.8.0

### New Components

- **Menubar** — Full headless menubar primitive with `BbMenubar`, `BbMenubarMenu`, `BbMenubarTrigger`, `BbMenubarContent`, `BbMenubarItem`, `BbMenubarCheckboxItem`, `BbMenubarLabel`, and `BbMenubarSeparator`. Includes coordinated open/close state, hover-to-switch between menus, and horizontal arrow-key navigation via `MenubarContext`.
- **NavigationMenu** — Headless navigation menu primitive with `BbNavigationMenu`, `BbNavigationMenuList`, `BbNavigationMenuItem`, `BbNavigationMenuTrigger`, `BbNavigationMenuContent`, and `BbNavigationMenuLink`. Features hover-based open/close with configurable delay timers, automatic close on navigation, and optional keyboard navigation via `NavigationMenuContext`.

### New Features

- **DataGrid** — Added `SearchText` property to `DataGridRequest`, enabling server-side providers to filter across all searchable columns using a global search term.
- **Menu keyboard JS** — Added `container` initial focus mode that focuses the menu container itself without selecting an item, allowing ArrowDown to navigate to the first item.
