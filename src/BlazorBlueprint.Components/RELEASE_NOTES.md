## What's New in v3.9.0

### New Features

- **Required parameter** added to selection and picker components: **BbCheckbox**, **BbCombobox**, **BbColorPicker**, **BbDatePicker**, **BbFileUpload**, **BbInputOTP**, **BbNativeSelect**, **BbRadioGroup**, **BbSelect**, and **BbTimePicker** now accept a `Required` bool parameter that sets the appropriate `aria-required` or `required` attribute for form validation and accessibility.
- **Required parameter** added to all corresponding **FormField** wrappers: **BbFormFieldCheckbox**, **BbFormFieldCombobox**, **BbFormFieldDatePicker**, **BbFormFieldFileUpload**, **BbFormFieldInputOTP**, **BbFormFieldNativeSelect**, **BbFormFieldRadioGroup**, **BbFormFieldSelect**, and **BbFormFieldTimePicker** now pass `Required` through to their inner components.

### Improvements

- **BbCheckbox** no longer infers `aria-required` from `CheckedExpression`; use the explicit `Required` parameter instead for clearer, more predictable behavior.
- Bumped **BlazorBlueprint.Primitives** dependency to v3.9.0.
