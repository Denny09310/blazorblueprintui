## What's New in v3.9.6

### Bug Fixes

- **BbFormFieldCombobox** — Fixed search filtering being bypassed when the combobox is used through the form field wrapper without explicitly binding `SearchQuery`. The inner combobox now correctly manages its own search state when the consumer does not provide an external search binding.
