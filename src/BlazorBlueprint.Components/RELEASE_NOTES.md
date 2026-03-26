## What's New in v3.9.0

### Breaking Changes

- **BbCheckbox** no longer infers `aria-required` from `CheckedExpression` binding — set `Required="true"` explicitly instead.

### New Features

- **Required parameter** added to selection and picker components: **BbSelect**, **BbNativeSelect**, **BbCombobox**, **BbDatePicker**, **BbTimePicker**, **BbColorPicker**, **BbInputOTP**, **BbFileUpload**, **BbCheckbox**, and **BbRadioGroup** now accept a `Required` parameter that renders the appropriate `required` or `aria-required` attribute for form validation and accessibility.
- **Required parameter** added to all corresponding **FormField** wrappers: **BbFormFieldSelect**, **BbFormFieldNativeSelect**, **BbFormFieldCombobox**, **BbFormFieldDatePicker**, **BbFormFieldTimePicker**, **BbFormFieldFileUpload**, **BbFormFieldInputOTP**, **BbFormFieldCheckbox**, **BbFormFieldRadioGroup** now pass `Required` through to their inner components.

### Improvements

- Bumped **BlazorBlueprint.Primitives** dependency to v3.9.0.
