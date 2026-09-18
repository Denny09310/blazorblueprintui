# Blazor Blueprint v4 Migration Guide

## .NET 10 minimum

Blazor Blueprint v4 requires .NET 10 or later. .NET 8 and .NET 9 are no longer supported. Install .NET 10 (the source repository uses SDK 10.0.400 or a later feature band), change consuming projects to `net10.0`, and update their Microsoft.AspNetCore.Components package references to 10.0.x before upgrading Bb. The library, icon packages, demo hosts and tests all target `net10.0`.

This guide helps you upgrade from Blazor Blueprint **v3** to **v4**. Breaking changes that require
code updates come first, followed by anything you can adopt at your own pace.

This guide is written as v4 is built, so it grows as changes land.

---

## Migration Checklist

| # | Breaking Change | Severity | Action Required |
|---|---|---|---|
| [0](#net-10-minimum) | .NET 10 minimum for all Bb packages | **High** | Retarget applications to `net10.0` or later; .NET 8/9 cannot consume v4 |
| [1](#1-bbdrawertrigger-and-bbdrawerclose-render-a-real-button) | `BbDrawerTrigger` / `BbDrawerClose` render a real `<button>` | **Medium** | Add `AsChild="true"` where the child is already a control |
| [2](#2-bbtooltiptriggeraschild-now-defaults-to-false) | `BbTooltipTrigger.AsChild` default → `false` | **Medium** | Add `AsChild="true"` where the child consumes the trigger context, such as a `BbButton` |
| [3](#3-every-utility-in-blazorblueprintcss-is-prefixed-bb) | Every utility in `blazorblueprint.css` is prefixed `bb:` | **Low** for most; **Medium** if you relied on the shipped utilities without your own Tailwind build | Nothing if you run Tailwind. Otherwise, see below |
| [4](#4-portal-host-components-moved-to-blazorblueprintprimitives) | The portal host components moved to the `BlazorBlueprint.Primitives` namespace | **Low** | Nothing if your `_Imports.razor` already has `@using BlazorBlueprint.Primitives`. Otherwise add it |
| [5](#5-navigationmenucontext-trigger-registration-is-keyed-by-the-trigger) | `NavigationMenuContext` trigger registration is keyed by the trigger | **Low** | Only affects code that drives the primitive directly. Pass the component instead of an index |
| [6](#6-parameters-that-never-did-anything-are-gone) | Parameters that never did anything are gone | **Low** | Delete them. None of them changed any behaviour |

---

## 1. `BbDrawerTrigger` and `BbDrawerClose` render a real `<button>`

**Issue:** [#507](https://github.com/blazorblueprintui/ui/issues/507)

Both were a bare `<div @onclick>` with no `tabindex`, no `role` and no keyboard handler. That
worked when the child happened to be focusable, and silently did not when it was not — plain text,
an icon or a `<span>` gave a trigger that no keyboard user could reach and no screen reader
announced as a control.

Both now render `<button type="button">` by default, with `AsChild="true"` for the case where the
child is already a control. This is the shape `BbDialogTrigger`, `BbSheetTrigger` and
`BbPopoverTrigger` have always had; Drawer was the only one that did not follow it.

### What to change

If you wrap a control, add `AsChild="true"`. Otherwise you get a `<button>` inside a `<button>`,
which is invalid HTML and which no browser renders reliably.

```razor
<!-- v3 -->
<BbDrawerTrigger>
    <BbButton Variant="ButtonVariant.Outline">Open</BbButton>
</BbDrawerTrigger>

<!-- v4 -->
<BbDrawerTrigger AsChild="true">
    <BbButton Variant="ButtonVariant.Outline">Open</BbButton>
</BbDrawerTrigger>
```

The same applies to `BbDrawerClose`.

**Plain content needs no change**, and now works by keyboard for the first time:

```razor
<BbDrawerTrigger>Open the drawer</BbDrawerTrigger>
```

### How to find every usage

Search for `<BbDrawerTrigger>` and `<BbDrawerClose>` with no `AsChild`, and check what the next
element is. If it is a component or a `<button>`, add `AsChild="true"`.

---

## 2. `BbTooltipTrigger.AsChild` now defaults to `false`

**Issue:** [#428](https://github.com/blazorblueprintui/ui/issues/428), deferred half of
[#425](https://github.com/blazorblueprintui/ui/issues/425)

In v3 this defaulted to `true`. In that mode the trigger renders no element and no handlers — it
only cascades a `TriggerContext`, and the child is required to consume it. `BbButton` does;
`LucideIcon` and plain markup do not. So the most natural thing to write silently did nothing:

```razor
<BbTooltipTrigger>
    <LucideIcon Name="house" />
</BbTooltipTrigger>
```

A default where the obvious usage fails, and the working usage requires knowing about an opt-out,
is the wrong way round. v3 moved trigger defaults to `true` across the family; for tooltip
specifically that turned out to be the wrong call, and v4 reverses it.

### What to change

Add `AsChild="true"` wherever the child consumes the trigger context itself:

```razor
<!-- v3 -->
<BbTooltipTrigger>
    <BbButton Variant="ButtonVariant.Outline">Hover me</BbButton>
</BbTooltipTrigger>

<!-- v4 -->
<BbTooltipTrigger AsChild="true">
    <BbButton Variant="ButtonVariant.Outline">Hover me</BbButton>
</BbTooltipTrigger>
```

A bare icon, plain text or arbitrary markup needs no change, and now works.

### What actually changes in the DOM

With `AsChild="false"` the trigger wraps its content in **two** nested `<span>` elements, not one:

```html
<span class="bb:contents">           <!-- styled wrapper: display: contents, no layout box -->
  <span id="…" tabindex="0"          <!-- the primitive trigger: an ordinary inline span -->
        aria-describedby="…">        <!-- this is what carries the hover/focus handlers -->
    …your content…
  </span>
</span>
```

The outer span uses `display: contents`, so it generates no layout box. The inner one does not — it
is an ordinary inline element, so it does establish a box. In flow layout that is usually
invisible, but inside a flex or grid container it becomes the flex item instead of your content,
and `inline-block` sizing applies to it rather than to the child. Set `Class` on the trigger to
give that wrapper the layout you need.

Anything that walks the DOM is affected either way: `:first-child` selectors, `querySelector` paths
and test hooks that assume the child is a direct descendant.

### Only the styled wrapper changed

`BlazorBlueprint.Primitives.Tooltip.BbTooltipTrigger` already defaulted to `false`. The divergence
was in the Components layer, and this removes it.

### The warning is still there

An unconsumed trigger context is reported through `ILogger` in the Development environment, added
in [#425](https://github.com/blazorblueprintui/ui/issues/425). With the default flipped you should
see it far less often, but it still catches an `AsChild="true"` around a child that ignores the
context.

---

## Other `AsChild` triggers

[#428](https://github.com/blazorblueprintui/ui/issues/428) asked whether the same default question
applies to popover, dialog, sheet, dropdown menu, hover card and collapsible. It does not, and they
are **unchanged**:

Those triggers render a `<button>` in their non-`AsChild` branch and are opened by a click, which
any focusable child already delivers by bubbling. Tooltip is different because it opens on **hover
and focus**, which do not bubble usefully — so a trigger that renders nothing genuinely has nothing
listening. The asymmetry is in the interaction, not in the API.

---

## 3. Every utility in `blazorblueprint.css` is prefixed `bb:`

**Issue:** [#501](https://github.com/blazorblueprintui/ui/issues/501), fixing
[#496](https://github.com/blazorblueprintui/ui/issues/496)

`blazorblueprint.css` is a prebuilt Tailwind stylesheet. In v3 its utilities were unprefixed and
written into Tailwind's `utilities` cascade layer — the same layer your own Tailwind build writes
into. Layer names are global to the document, so two builds emitting the same class name into the
same layer were resolved by which `<link>` came second, not by Tailwind's sort order. Your
`sm:grid-cols-2 md:grid-cols-4` collapsed when Blazor Blueprint loaded after your stylesheet, and
the library's own `hidden sm:flex` collapsed when it loaded before. No load order fixed both.

In v4 every utility the library emits is prefixed — `.bb\:flex`, `.bb\:sm\:hidden`,
`.bb\:data-\[state\=open\]\:bg-accent` — and lives in a `bb-utilities` layer of its own. The two
builds can no longer produce the same class name, so nothing depends on load order any more.

### If you run your own Tailwind build

**Nothing changes in your markup.** `Class="p-6"` is still `p-6`; the library strips its prefix
when it merges, so your unprefixed class still replaces the library's for the same property:

```razor
<BbCard Class="p-6">        @* renders class="… bb:rounded-lg bb:border … p-6" *@
```

Two things to check:

- **Remove any `@source` that points at the Blazor Blueprint package or sources.** It was never
  needed, and under v4 it finds `bb:flex`, does not recognise the `bb` variant, and emits nothing.
- **Load order no longer matters** for utilities. Keep your theme before `blazorblueprint.css` as
  before; put your Tailwind output wherever you like.

### If you do not run Tailwind

Some projects wrote Tailwind classes in their own markup and relied on `blazorblueprint.css`
happening to contain them. That was never supported — the file only ever held the classes the
components use — and in v4 those classes are all prefixed, so a bare `class="flex gap-4"` in your
page matches nothing.

You have two options:

- **Add a Tailwind build to your project.** This is the supported path for using utilities in your
  own markup. The [standalone CLI](https://tailwindcss.com/blog/standalone-cli) needs no Node.js.
- **Use the prefixed classes directly:** `class="bb:flex bb:gap-4"`. They work anywhere on the
  page, not only inside components. The set is whatever the components happen to use and may
  change between versions, so treat this as a stopgap rather than an API.

### Renamed: `shimmer` and `scroll-fade-x`

These two utilities were safelisted so consumers could apply them by name. They are now
`bb:shimmer` and `bb:scroll-fade-x`:

```razor
<!-- v3 -->
<BbMarkerContent Class="shimmer">…</BbMarkerContent>

<!-- v4 -->
<BbMarkerContent Class="bb:shimmer">…</BbMarkerContent>
```

### Internal class names are not an API

If you have CSS, JavaScript or tests that select the library's internal elements by utility class
(`.flex-col`, `.group\/row`, `.hidden`), those selectors now need the prefix. Prefer the `data-slot`
and other data attributes the components render; those are stable.

## 4. Portal host components moved to `BlazorBlueprint.Primitives`

`BbPortalHost`, `BbContainerPortalHost`, `BbOverlayPortalHost` and `BbCategoryPortalHost` were in
`BlazorBlueprint.Primitives.Services`. They are now in `BlazorBlueprint.Primitives`, alongside every
other primitive component. The services themselves — `IPortalService`, `PortalService`,
`PortalCategory` — have not moved.

### What to change

Nothing, if your `_Imports.razor` follows the documented setup:

```razor
@using BlazorBlueprint.Components
@using BlazorBlueprint.Primitives
```

If a file imports only `BlazorBlueprint.Primitives.Services` and writes `<BbPortalHost />`, add
`@using BlazorBlueprint.Primitives` to it.

### Why this is worth a breaking change

A Razor tag that does not resolve to a component is not an error. The compiler emits it as a literal
HTML element, so `<BbPortalHost />` became `<bbportalhost>`: no host registered, no overlay ever
rendered, and no build output pointing at the cause. The only visible symptom was a runtime warning
saying the host was missing from a layout that plainly contained one. Reported in
[#545](https://github.com/blazorblueprintui/ui/issues/545), where the giveaway was that adding
`@rendermode` to the tag failed with `RZ10023: Attribute '@rendermode' is only valid when used on a
component`.

Putting the host in the namespace people already import removes the trap. Two other things guard it
now: the warning walks through the `@using` check first, and both READMEs carry the using in their
setup snippets.

> This is the same move v3 made for eight other consumer-facing types, which is why a v3 application
> that followed that migration already has the right using.

## 5. `NavigationMenuContext` trigger registration is keyed by the trigger

Only affects code that drives the `NavigationMenu` primitive directly. If you use `BbNavigationMenu`
and its parts, there is nothing to do.

| Removed | Replacement |
|---|---|
| `int RegisterTrigger(ElementReference)` | `void RegisterTrigger(object owner, ElementReference)` |
| `void UpdateTriggerRef(int index, ElementReference)` | `RegisterTrigger(owner, triggerRef)` again — it replaces the reference it already holds |
| — | `void UnregisterTrigger(object owner)` |
| — | `int TriggerIndexOf(object owner)` |

### What to change

Pass the trigger component as its own identity, and read its index when you need one rather than
remembering the one you were handed:

```razor
@* Before *@
@code {
    private int triggerIndex;

    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender) { triggerIndex = Menu.RegisterTrigger(element); }
        else { Menu.UpdateTriggerRef(triggerIndex, element); }
    }
}

@* After *@
@implements IDisposable
@code {
    protected override void OnAfterRender(bool firstRender) => Menu.RegisterTrigger(this, element);

    private int Index => Menu.TriggerIndexOf(this);

    public void Dispose() => Menu.UnregisterTrigger(this);
}
```

### Why it changed

An index-based list cannot express removal: taking an entry out shifts every index already handed
out, so nothing was ever unregistered. Triggers that had left the page stayed in the list, and
arrow-key navigation kept stepping onto buttons that no longer existed. A remembered index is the
same bug in consumer code, which is why `TriggerIndexOf` is a lookup rather than a value you keep.

`GetTriggerAt(int)` and `TriggerCount` are unchanged.

---

## 6. Parameters that never did anything are gone

Each of these was accepted and then ignored. Removing them changes no behaviour — it just turns a
silent no-op into a compile error. Delete the attribute.

| Removed | Why it did nothing | What to use instead |
|---|---|---|
| `BbCalendar.Mode` and the `CalendarMode` enum | `BbCalendar` is single-select; the mode was never read | `BbDateRangePicker` for a range |
| `BbCommand.CloseOnSelect` | The dialog is what closes, not the command list | `BbCommandDialog.CloseOnSelect` |
| `Stacked` and `StackGroup` on `BbPie`, `BbFunnel`, `BbGauge`, `BbRadar`, `BbHeatmap` and `BbCandlestick` | None of these series types can stack | Nothing — stacking now lives on `StackableSeriesBase`, so the parameters appear only on `BbBar`, `BbLine`, `BbArea`, `BbScatter` and `BbRadialBar`, where they work |

These are the only public members removed in v4. Everything else in the surface is additive or a
namespace move (see [4](#4-portal-host-components-moved-to-blazorblueprintprimitives)).

---

## New v4 editing and scheduling APIs

- `BbDataGrid` adds `DataGridEditMode.Cell` and `Batch`. Supply `EditItemFactory` to make independent editable DTO copies, including nested objects. Existing `Row` mode continues binding to the original row. Cell callbacks receive a draft as `Item` and the source as `OriginalItem`; batch callbacks receive all `Changes` and must persist them atomically. Failed validation or rejected saves retain drafts. Use stable, uneditable `ItemKey` values.
- `BbScheduler` adds day/week time slots, resource lanes, an event editor, recurrence and time zones. It is separate from `BbEventCalendar`. Bind `Events`, use `OnEventChange` for persistence, and set `Cancel` to reject. Start/End are instants; an event's IANA time zone governs recurrence. Ical.Net 5.2.3 expands daily/weekly/monthly/yearly RRULEs, with occurrence exclusions and overrides. Timed events keep their elapsed duration across DST. The editor distinguishes repeated start/end times and rejects skipped times.
- `BbTreeSelect<TItem>` and `BbCascader<TItem>` accept nested `Items` plus key/text/children selectors. Bind stable string keys through `Value`, or `Values` in TreeSelect multiple mode. `ValueExpression`/`ValuesExpression` integrate with `EditContext`.
- `BbFileUpload` remains a file selector when `UploadHandler` is null. With a handler, automatic uploads report `FileUploadItem.Status`, `BytesTransferred` and `Progress`. Observe the cancellation token and report cumulative bytes through `FileUploadContext.ReportProgressAsync`. Retry opens a fresh stream from byte zero. Retained hidden inputs preserve browser file handles across subsequent selections; removing/clearing files cancels their attempts.

Live demos and complete code examples are available at `/components/datagrid-editing`, `/components/scheduler`, `/components/tree-select`, `/components/cascader` and `/components/file-upload`.

## Building release packages

Build and pack the current projects together by default; their project references keep the Components and Primitives APIs aligned. For the release workflow that switches to published package references, pass both `-p:UsePackageReferences=true` and `-p:PrimitivesPackageVersion=<matching .NET 10 v4 release>`. Publish that Primitives version first. The old hard-coded `4.0.0-beta.5` reference cannot supply the new editing APIs and has been removed. No packages are published by a local build or pack.
