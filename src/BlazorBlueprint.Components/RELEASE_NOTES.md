## What's New in v3.6.1

### New Features

- **FileUpload** — Added `ClearFiles()` method to programmatically clear all files and validation errors, resetting the component to its initial state.
- **FormFieldFileUpload** — Exposed `ClearFiles()` method to allow clearing files through the form field wrapper.

### Improvements

- **TailwindMerge** — Replaced custom TailwindMerge implementation with the [TailwindMerge.NET](https://www.nuget.org/packages/TailwindMerge.NET) package for more reliable and up-to-date class merging.
- **ClassNames** — Added CSS class name validation to reject values that could be used for CSS injection attacks.

### Bug Fixes

- **BlazorBlueprint.Primitives** — Bumped dependency to v3.6.1 (includes fix for FormFieldSelect dropdown reopening on second trigger click).
