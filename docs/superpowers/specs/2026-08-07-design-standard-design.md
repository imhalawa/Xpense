# Xpense design standard

Date: 2026-08-07
Status: agreed, not yet implemented
Scope: `web/` only. No API changes.

## Why

The web app was built two years ago and has no design standard. Its MUI theme sets a font
family, a base font size and breakpoints — nothing else. Every other visual decision lives in
ad-hoc `sx` props on individual components. The Dashboard renders three of its four charts from
a hardcoded `areaChartData` constant.

This document fixes the visual vocabulary so later screens are built against a standard instead
of re-deciding colour and spacing per component. It deliberately does not decide which pages
exist, which analytics they show, or how transactions get entered. Those are two separate specs.

## Decisions

### Character: soft cards

Rounded surfaces, soft shadows, a filled brand-blue hero tile, generous padding. Chosen over a
dense bordered "instrument" look and over a high-contrast editorial look.

Known cost: soft cards show roughly half the rows per screen compared to the dense option, and
the transactions list is long. Mitigated by two density tokens rather than by abandoning the
character.

### Device support: full parity

Desktop and mobile both get first-class layouts. Accepted cost: roughly double the layout work
per screen, and every component needs two states designed.

### Typeface: Manrope

`@fontsource/manrope` 5.3.0, variable, self-hosted. Replaces Lato.

Lato was rejected on measurement, not taste. Inspecting the font binaries for OpenType numeral
feature tags showed Lato contains neither `tnum` nor `pnum`, so `font-variant-numeric:
tabular-nums` has no effect and columns of amounts cannot be made to align — browsers do not
synthesise tabular figures. Manrope, Inter and Plus Jakarta Sans all contain both tags.

Manrope's lowercase is slightly awkward at 14px, so the body base is **15px** rather than
pairing a second font for body text. One font, one file.

### Colour scheme: light and dark, plus a manual toggle

Both schemes follow the OS setting, with an override in Settings. MUI 9.3.1 supports this
natively — `cssVariables`, `colorSchemes`, `InitColorSchemeScript` and `useColorScheme` are all
present in the installed version, so no custom context is needed.

Dark mode is **selected, not derived**. Every hue is re-stepped for the dark surface and
re-validated. Elevation switches channel entirely: shadows in light mode, surface lightness in
dark mode, because soft shadows are invisible on a dark background.

### Brand primary

`#1565C0` is kept. It passes as categorical slot 1 on a white surface — lightness band, chroma
floor, CVD separation (worst adjacent ΔE 9.1), normal-vision floor (19.6). On the dark surface it
reaches only 2.97:1, so it steps to `#3987e5` there.

| Step | Hex | Role |
|---|---|---|
| 50 | `#E8F1FB` | tint, icon tile background |
| 100 | `#cde2fb` | selected row |
| 200 | `#9ec5f4` | |
| 300 | `#6da7ec` | |
| 400 | `#3987e5` | brand primary, dark mode |
| 500 | `#1565C0` | brand primary, light mode |
| 600 | `#184f95` | hover, pressed |
| 700 | `#0d366b` | |

### Category colours

Eight fixed slots, assigned in order and never cycled. A ninth category folds into "Other"
rather than generating a hue. Slot 1 is the brand blue.

| Slot | Hue | Light | Dark |
|---|---|---|---|
| 1 | blue | `#1565C0` | `#3987e5` |
| 2 | orange | `#eb6834` | `#d95926` |
| 3 | aqua | `#1baf7a` | `#199e70` |
| 4 | yellow | `#eda100` | `#c98500` |
| 5 | magenta | `#e87ba4` | `#d55181` |
| 6 | green | `#008300` | `#008300` |
| 7 | violet | `#4a3aa7` | `#9085e9` |
| 8 | red | `#e34948` | `#e66767` |

Light: worst adjacent CVD ΔE 9.1, worst adjacent normal-vision ΔE 19.6. Three light slots
(aqua, yellow, magenta) sit below 3:1 against white, so charts using them ship visible direct
labels or a table view. Dark: all eight clear 3:1, worst adjacent CVD ΔE 8.4.

Scatter, bubble and small-multiple forms — where every pair is visible at once, not just
adjacent ones — cap at the **first three slots**. Past three, fold to "Other" or facet.

### Income and expense: blue and red

`#2a78d6` ↔ `#d03b3b`, the reference palette's documented diverging pair. Measured against the
alternatives:

| Pair | CVD ΔE | Light contrast | Dark contrast |
|---|---|---|---|
| blue ↔ red | **23.8** | pass | pass |
| teal ↔ red | 9.9 | warn, teal 2.82:1 | pass |
| green ↔ red | **4.1 — fail** | pass | pass |

Green/red, the universal money convention, is the worst available option for colour-blind
readers. Blue/red has 2.4× the separation of teal/red and is the only pair clearing 3:1 in both
themes.

### Colour reach: every amount, with two red steps

Every amount is coloured, so polarity reads even when a row is scanned in isolation. Because
that makes red ambient, ordinary expenses use a **muted** red and alerts use a **saturated** red
plus a filled wash and a direction arrow.

| Role | Light | on white | Dark | on `#191C21` |
|---|---|---|---|---|
| Primary ink | `#1a2230` | 15.96 | `#e8eaee` | 14.18 |
| Expense, ordinary | `#9a5252` | 5.67 | `#cf8f8f` | 6.49 |
| Expense, alert | `#d03b3b` | 4.80 | `#e66767` | 5.29 |
| Income | `#1c5cab` | 6.63 | `#7fb4f0` | 7.88 |

The muted red has *higher* contrast than the alert red. That is deliberate: ordinary amounts are
the text read most often, so readability outranks a tidy hierarchy. Escalation is carried by
chroma, the chip wash and the arrow icon — never by contrast alone.

### Delta chip

A named component. Tinted background, direction arrow, text. Used for budget deltas and period
comparisons. All four variants pass AA body text.

| Variant | Light | Ratio | Dark | Ratio |
|---|---|---|---|---|
| Over budget | `#a62020` on `#fdeaea` | 6.37 | `#f2a3a3` on `#3a2323` | 7.30 |
| Comparison | `#124e96` on `#e8f1fb` | 7.20 | `#a8cbf5` on `#1e2c3e` | 8.43 |

### Type ramp

Manrope, 15px base.

| Role | Size / weight | Notes |
|---|---|---|
| Hero number | 32px / 700, −0.02em | 28px on mobile |
| Page title | 24px / 700 | |
| Section | 20px / 600 | |
| Card title | 16px / 700 | |
| Body | 15px / 400, 1.55 | |
| Secondary | 13px / 400 | |
| Label, overline | 11px / 700, uppercase, .09em | |
| Numeric column | 15px / 600, `tabular-nums` | table rows and axis ticks only |

Large standalone numbers use default proportional figures. `tabular-nums` is reserved for
columns that must align vertically.

### Spacing

MUI's 8px unit with half-steps: 4 · 8 · 12 · 16 · 24 · 32 · 48 · 64.

### Radius

| Element | Radius |
|---|---|
| Card, surface | 16px |
| Input, select, menu | 12px |
| Icon tile | 12px |
| Primary action | 999px |
| Chip | 999px |
| Chart bar ends | 4px, anchored to baseline |

### Elevation

| Level | Light | Dark |
|---|---|---|
| Page plane | `#f5f7fa` | `#0F1115` |
| 1 · card | `0 2px 10px rgba(26,34,48,.07)` on `#fff` | `#191C21`, no shadow |
| 2 · raised | `0 8px 20px rgba(26,34,48,.10)` | `#20242B` |
| 3 · menu, dialog | `0 16px 32px rgba(26,34,48,.16)` | `#272C34` |
| Hairline ring | `rgba(26,34,48,.08)` | `rgba(255,255,255,.08)` |

### Density

| Token | Row height | Body | Used on |
|---|---|---|---|
| `comfortable` | 56px | 15px | dashboard, forms, detail |
| `compact` | 40px | 14px | transactions grid |

On mobile the transactions grid becomes a card list rather than a table: a 40px row cannot hold
a 44px touch target, and full device parity was chosen.

### Motion

120ms fast, 200ms base, 320ms slow. `cubic-bezier(.2,0,0,1)` entering, `cubic-bezier(.4,0,1,1)`
leaving. All motion disabled under `prefers-reduced-motion`.

### Focus ring

2px brand blue, 2px offset, on every interactive element. Absent from the app today.

## Chart rules that follow from the palette

- One y-axis. Never a dual-axis chart. Two measures of different scale become two charts.
- Colour follows the entity, never its rank. A filter that changes the series count must not
  repaint the survivors.
- Sequential encoding is one hue light-to-dark. Diverging is blue/red with a neutral gray
  midpoint. Never a rainbow.
- Two or more series always carry a legend; four or fewer are also direct-labelled, so identity
  is never colour-alone. A single series needs no legend — the title names it.
- Values, labels and legend text wear ink tokens, never the series colour.
- Line and area charts get a crosshair and tooltip; bar, dot and cell charts get a per-mark
  tooltip. Only a bare stat tile with no plot skips this.

## Out of scope

- Which pages exist, navigation shape, and which analytics live where. Separate spec.
- How transactions get entered, and how form components stay relevant alongside AI. Separate
  spec.
- New analytics API endpoints. The API currently exposes exactly one analytics endpoint,
  `GET /api/v1/analytics/spending/by-category`, with no date-range parameter. Any real analytics
  work needs API changes, which the IA spec must enumerate.
- The Budgets and Notifications features. The API implements both in full; the web app uses
  neither. Wiring them up belongs to the IA spec.

## Verification

The categorical palette and the diverging pair were validated with the dataviz skill's
`validate_palette.js` against Xpense's own surfaces, not the script's defaults:

```
node scripts/validate_palette.js "#1565C0,#eb6834,#1baf7a,#eda100,#e87ba4,#008300,#4a3aa7,#e34948" \
  --mode light --surface "#FFFFFF"
node scripts/validate_palette.js "#3987e5,#d95926,#199e70,#c98500,#d55181,#008300,#9085e9,#e66767" \
  --mode dark --surface "#191C21"
```

Text and chip ratios are plain WCAG contrast, computed rather than estimated. Re-run both if any
hex or either surface changes.

Font feature tags were checked by grepping the font binaries for `tnum` and `pnum`. That is a
strong signal, not a full GSUB parse; a `fontTools` check would be conclusive if it ever matters.

## Implementation notes

- One theme module exporting a `createTheme` call with `cssVariables` and both `colorSchemes`.
  Tokens live there, not in `sx` props.
- The repo standard is no comments in any file. Token names carry the meaning instead.
- `.superpowers/` is not in `.gitignore`; the brainstorming mockups under it are untracked.
