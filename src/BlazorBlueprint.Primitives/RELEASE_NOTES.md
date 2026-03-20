## What's New in v3.6.1

### Bug Fixes

- **Select** — Fixed dropdown reopening when clicking the trigger button a second time to close it. The trigger now correctly syncs its element ID with the select context, ensuring click-outside detection works properly when a custom `id` attribute is present (e.g. inside form fields).
