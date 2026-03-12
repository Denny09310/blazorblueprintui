## What's New in v3.5.2

### New Features

- **DateRangePicker** — Customizable quick-pick presets via new `Presets` parameter and `DateRangeQuickPick` class, supporting both built-in presets (with automatic localization) and fully custom entries with user-supplied labels and range factories
- **DateRangePicker** — Mobile preset picker now uses a **BbNativeSelect** dropdown instead of a horizontal scrolling button strip for improved mobile UX
- **DateRangePicker** — Desktop preset sidebar scrolls independently when many presets overflow the available space

### Bug Fixes

- **NativeSelect** — Fix chevron icon not rendering under Tailwind v4 by moving the SVG data URL from arbitrary-value classes to a dedicated CSS rule
- **NativeSelect** — Fix nullable `TValue` handling by using `Nullable.GetUnderlyingType` before type conversion
- **NativeSelect** — Fix placeholder not displaying correctly for both value types and reference types
- **SidebarProvider** — Add `bg-sidebar` background to the wrapper div for the inset variant to prevent background bleed-through

### Improvements

- **DateRangePicker** — Preset buttons now use `BbButton` (Ghost/Small) instead of raw HTML buttons for consistency
