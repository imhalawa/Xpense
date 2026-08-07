# Design Standard Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the web app's near-empty MUI theme with the validated design standard — tokens, typography, two colour schemes, density, and the delta chip — so every later screen is built against a standard instead of ad-hoc `sx` props.

**Architecture:** A single `theme/` directory owns every visual decision. `tokens.ts` holds raw values as plain data; `theme.ts` feeds them to `createTheme` with `cssVariables` and both `colorSchemes`. Tests assert the measured contrast ratios from the spec, so the standard cannot silently rot. No component reads a raw hex — components read theme roles.

**Tech Stack:** React 19, MUI 9.3.1, `@fontsource/manrope` 5.3.0, Vitest (new), TypeScript 7, Vite 8.

## Global Constraints

- Source spec: `docs/superpowers/specs/2026-08-07-design-standard-design.md`. Every hex and ratio in this plan is copied verbatim from it.
- **No comments in any file.** Project-wide rule. Token names carry the meaning instead. The only exception in the codebase is Swagger DTO `<summary>` docs, which do not apply to the web project.
- **No barrel value imports in `src/typings`.** A circular initialisation crash has happened before; `tsc` and `vite build` both miss it. `npm run check:init` catches it and must pass.
- Explicit names. No abbreviations like `ct`, `db`, `cfg`.
- Brand primary is `#1565C0` in light and `#3987e5` in dark. Never hardcoded outside `tokens.ts`.
- Body base font size is 15px, not 14px.
- Light chart/card surface `#FFFFFF`, light page plane `#f5f7fa`. Dark card surface `#191C21`, dark page plane `#0F1115`.

## File Structure

| File | Responsibility |
|---|---|
| `web/vitest.config.ts` | Test runner configuration |
| `web/src/theme/tokens.ts` | Raw token values as plain data. No MUI imports |
| `web/src/theme/tokens.test.ts` | Asserts the spec's measured contrast ratios |
| `web/src/theme/contrast.ts` | WCAG contrast helper, used by tests and by nothing else |
| `web/src/theme/theme.ts` | `createTheme` call wiring tokens into MUI |
| `web/src/theme/density.ts` | `comfortable` and `compact` row metrics |
| `web/src/components/DeltaChip/DeltaChip.tsx` | The delta chip component |
| `web/src/components/DeltaChip/DeltaChip.test.tsx` | Its tests |
| `web/src/components/ThemeModeToggle/ThemeModeToggle.tsx` | Light/dark/system toggle |
| `web/src/App.tsx` | Modify: consume `theme.ts`, drop the inline `createTheme` |
| `web/src/index.css` | Modify: Manrope imports replace Lato |
| `web/package.json` | Modify: add Vitest, add `@fontsource/manrope`, remove `@fontsource/lato` |

---

### Task 1: Test infrastructure

The web project has no test runner and no test files. Everything after this depends on it.

**Files:**
- Create: `web/vitest.config.ts`
- Create: `web/src/theme/contrast.ts`
- Create: `web/src/theme/contrast.test.ts`
- Modify: `web/package.json`

**Interfaces:**
- Consumes: nothing
- Produces: `contrastRatio(foreground: string, background: string): number` from `src/theme/contrast.ts`. Tasks 2 and 6 assert against it. `npm test` runs Vitest.

- [ ] **Step 1: Install Vitest**

```bash
cd web && npm install --save-dev vitest@^3 jsdom@^25 @testing-library/react@^16 @testing-library/jest-dom@^6
```

- [ ] **Step 2: Add the test script**

In `web/package.json`, add to `scripts`:

```json
"test": "vitest run",
"test:watch": "vitest"
```

- [ ] **Step 3: Create the Vitest config**

`web/vitest.config.ts`:

```ts
import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react-swc";

export default defineConfig({
  plugins: [react()],
  test: {
    environment: "jsdom",
    globals: true,
    include: ["src/**/*.test.{ts,tsx}"],
  },
});
```

- [ ] **Step 4: Write the failing test**

`web/src/theme/contrast.test.ts`:

```ts
import { describe, expect, it } from "vitest";
import { contrastRatio } from "./contrast";

describe("contrastRatio", () => {
  it("returns 21 for black on white", () => {
    expect(contrastRatio("#000000", "#FFFFFF")).toBeCloseTo(21, 1);
  });

  it("returns 1 for a colour against itself", () => {
    expect(contrastRatio("#1565C0", "#1565C0")).toBeCloseTo(1, 2);
  });

  it("is symmetric", () => {
    expect(contrastRatio("#1565C0", "#FFFFFF")).toBeCloseTo(
      contrastRatio("#FFFFFF", "#1565C0"),
      5
    );
  });
});
```

- [ ] **Step 5: Run it to verify it fails**

Run: `cd web && npx vitest run src/theme/contrast.test.ts`
Expected: FAIL — cannot resolve `./contrast`.

- [ ] **Step 6: Write the minimal implementation**

`web/src/theme/contrast.ts`:

```ts
const channels = (hex: string): number[] => {
  const value = parseInt(hex.slice(1), 16);
  return [(value >> 16) & 255, (value >> 8) & 255, value & 255].map((part) => part / 255);
};

const linearise = (channel: number): number =>
  channel <= 0.03928 ? channel / 12.92 : ((channel + 0.055) / 1.055) ** 2.4;

const relativeLuminance = (hex: string): number => {
  const [red, green, blue] = channels(hex).map(linearise);
  return 0.2126 * red + 0.7152 * green + 0.0722 * blue;
};

export const contrastRatio = (foreground: string, background: string): number => {
  const [lighter, darker] = [relativeLuminance(foreground), relativeLuminance(background)].sort(
    (first, second) => second - first
  );
  return (lighter + 0.05) / (darker + 0.05);
};
```

- [ ] **Step 7: Run it to verify it passes**

Run: `cd web && npm test`
Expected: PASS, 3 tests.

- [ ] **Step 8: Commit**

```bash
git add web/package.json web/package-lock.json web/vitest.config.ts web/src/theme/contrast.ts web/src/theme/contrast.test.ts
git commit -m "test: add vitest and a wcag contrast helper"
```

---

### Task 2: Colour tokens with contrast assertions

The tests here are the point: they encode the spec's measured ratios so a future edit that breaks accessibility fails the build.

**Files:**
- Create: `web/src/theme/tokens.ts`
- Create: `web/src/theme/tokens.test.ts`

**Interfaces:**
- Consumes: `contrastRatio` from Task 1.
- Produces: `tokens` object with shape `{ brand, category, priority, money, chip, surface, ink }`, each keyed `light` / `dark` where mode-dependent. Task 3 consumes `tokens`. Task 6 consumes `tokens.chip`.

- [ ] **Step 1: Write the failing test**

`web/src/theme/tokens.test.ts`:

```ts
import { describe, expect, it } from "vitest";
import { contrastRatio } from "./contrast";
import { tokens } from "./tokens";

describe("ink and money tokens", () => {
  const cases: [string, string, string, number][] = [
    ["primary ink light", tokens.ink.light.primary, tokens.surface.light.card, 15.9],
    ["primary ink dark", tokens.ink.dark.primary, tokens.surface.dark.card, 14.1],
    ["expense ordinary light", tokens.money.light.expenseOrdinary, tokens.surface.light.card, 5.6],
    ["expense ordinary dark", tokens.money.dark.expenseOrdinary, tokens.surface.dark.card, 6.4],
    ["expense alert light", tokens.money.light.expenseAlert, tokens.surface.light.card, 4.5],
    ["expense alert dark", tokens.money.dark.expenseAlert, tokens.surface.dark.card, 5.2],
    ["income light", tokens.money.light.income, tokens.surface.light.card, 6.6],
    ["income dark", tokens.money.dark.income, tokens.surface.dark.card, 7.8],
  ];

  it.each(cases)("%s clears AA body text", (_name, foreground, background, floor) => {
    expect(contrastRatio(foreground, background)).toBeGreaterThanOrEqual(floor);
  });
});

describe("delta chip tokens", () => {
  const cases: [string, string, string][] = [
    ["over budget light", tokens.chip.light.overBudget.text, tokens.chip.light.overBudget.wash],
    ["comparison light", tokens.chip.light.comparison.text, tokens.chip.light.comparison.wash],
    ["over budget dark", tokens.chip.dark.overBudget.text, tokens.chip.dark.overBudget.wash],
    ["comparison dark", tokens.chip.dark.comparison.text, tokens.chip.dark.comparison.wash],
  ];

  it.each(cases)("%s clears AA body text on its own wash", (_name, text, wash) => {
    expect(contrastRatio(text, wash)).toBeGreaterThanOrEqual(4.5);
  });
});

describe("category palette", () => {
  it("has exactly eight slots in both modes", () => {
    expect(tokens.category.light).toHaveLength(8);
    expect(tokens.category.dark).toHaveLength(8);
  });

  it("opens on the brand blue in light mode", () => {
    expect(tokens.category.light[0]).toBe("#1565C0");
  });

  it("steps the brand blue lighter in dark mode for contrast", () => {
    expect(tokens.category.dark[0]).toBe("#3987e5");
    expect(contrastRatio(tokens.category.dark[0], tokens.surface.dark.card)).toBeGreaterThanOrEqual(3);
  });
});

describe("priority ordinal ramp", () => {
  it("has one step per seeded priority", () => {
    expect(tokens.priority.light).toHaveLength(5);
    expect(tokens.priority.dark).toHaveLength(5);
  });

  it("darkens monotonically from Extreme to None in light mode", () => {
    const ratios = tokens.priority.light.map((step) => contrastRatio(step, tokens.surface.light.card));
    const descending = [...ratios].sort((first, second) => second - first);
    expect(ratios).toEqual(descending);
  });
});
```

- [ ] **Step 2: Run it to verify it fails**

Run: `cd web && npx vitest run src/theme/tokens.test.ts`
Expected: FAIL — cannot resolve `./tokens`.

- [ ] **Step 3: Write the tokens**

`web/src/theme/tokens.ts`:

```ts
export const tokens = {
  brand: {
    50: "#E8F1FB",
    100: "#cde2fb",
    200: "#9ec5f4",
    300: "#6da7ec",
    400: "#3987e5",
    500: "#1565C0",
    600: "#184f95",
    700: "#0d366b",
  },
  surface: {
    light: { page: "#f5f7fa", card: "#FFFFFF", raised: "#FFFFFF", overlay: "#FFFFFF" },
    dark: { page: "#0F1115", card: "#191C21", raised: "#20242B", overlay: "#272C34" },
  },
  ink: {
    light: { primary: "#1a2230", secondary: "#5b6879", muted: "#748091" },
    dark: { primary: "#e8eaee", secondary: "#a7b0bd", muted: "#8b95a3" },
  },
  money: {
    light: { income: "#1c5cab", expenseOrdinary: "#9a5252", expenseAlert: "#d03b3b" },
    dark: { income: "#7fb4f0", expenseOrdinary: "#cf8f8f", expenseAlert: "#e66767" },
  },
  series: {
    light: { income: "#2a78d6", expense: "#d03b3b" },
    dark: { income: "#3987e5", expense: "#e66767" },
  },
  chip: {
    light: {
      overBudget: { text: "#a62020", wash: "#fdeaea" },
      comparison: { text: "#124e96", wash: "#e8f1fb" },
    },
    dark: {
      overBudget: { text: "#f2a3a3", wash: "#3a2323" },
      comparison: { text: "#a8cbf5", wash: "#1e2c3e" },
    },
  },
  category: {
    light: ["#1565C0", "#eb6834", "#1baf7a", "#eda100", "#e87ba4", "#008300", "#4a3aa7", "#e34948"],
    dark: ["#3987e5", "#d95926", "#199e70", "#c98500", "#d55181", "#008300", "#9085e9", "#e66767"],
  },
  priority: {
    light: ["#104281", "#1c5cab", "#2a78d6", "#5598e7", "#86b6ef"],
    dark: ["#9ec5f4", "#6da7ec", "#3987e5", "#256abf", "#184f95"],
  },
  radius: { card: 16, input: 12, iconTile: 12, pill: 999, chartBarEnd: 4 },
  motion: {
    fast: 120,
    base: 200,
    slow: 320,
    easingEnter: "cubic-bezier(0.2, 0, 0, 1)",
    easingExit: "cubic-bezier(0.4, 0, 1, 1)",
  },
} as const;
```

- [ ] **Step 4: Run it to verify it passes**

Run: `cd web && npm test`
Expected: PASS. If any contrast assertion fails, the token is wrong — not the test.

- [ ] **Step 5: Verify no circular initialisation**

Run: `cd web && npm run check:init`
Expected: exits 0.

- [ ] **Step 6: Commit**

```bash
git add web/src/theme/tokens.ts web/src/theme/tokens.test.ts
git commit -m "feat: add design tokens with contrast assertions"
```

---

### Task 3: The MUI theme

**Files:**
- Create: `web/src/theme/theme.ts`
- Create: `web/src/theme/theme.test.ts`
- Modify: `web/src/App.tsx`

**Interfaces:**
- Consumes: `tokens` from Task 2.
- Produces: default export `theme` from `src/theme/theme.ts`, a `Theme` built with `cssVariables: { colorSchemeSelector: "data-theme" }` and both `colorSchemes`. Tasks 5, 6 and 7 consume it.

- [ ] **Step 1: Write the failing test**

`web/src/theme/theme.test.ts`:

```ts
import { describe, expect, it } from "vitest";
import theme from "./theme";
import { tokens } from "./tokens";

describe("theme", () => {
  it("declares both colour schemes", () => {
    expect(theme.colorSchemes.light).toBeDefined();
    expect(theme.colorSchemes.dark).toBeDefined();
  });

  it("uses the brand blue as light primary and the lighter step as dark primary", () => {
    expect(theme.colorSchemes.light.palette.primary.main).toBe(tokens.brand[500]);
    expect(theme.colorSchemes.dark.palette.primary.main).toBe(tokens.brand[400]);
  });

  it("sets the body base size to fifteen pixels", () => {
    expect(theme.typography.fontSize).toBe(15);
  });

  it("names Manrope first in the font stack", () => {
    expect(theme.typography.fontFamily).toMatch(/^"Manrope"/);
  });

  it("rounds cards to the token radius", () => {
    expect(theme.shape.borderRadius).toBe(tokens.radius.card);
  });
});
```

- [ ] **Step 2: Run it to verify it fails**

Run: `cd web && npx vitest run src/theme/theme.test.ts`
Expected: FAIL — cannot resolve `./theme`.

- [ ] **Step 3: Write the theme**

`web/src/theme/theme.ts`:

```ts
import { createTheme } from "@mui/material/styles";
import { tokens } from "./tokens";

const theme = createTheme({
  cssVariables: { colorSchemeSelector: "data-theme" },
  colorSchemes: {
    light: {
      palette: {
        mode: "light",
        primary: { main: tokens.brand[500], dark: tokens.brand[600], light: tokens.brand[300] },
        background: { default: tokens.surface.light.page, paper: tokens.surface.light.card },
        text: {
          primary: tokens.ink.light.primary,
          secondary: tokens.ink.light.secondary,
          disabled: tokens.ink.light.muted,
        },
      },
    },
    dark: {
      palette: {
        mode: "dark",
        primary: { main: tokens.brand[400], dark: tokens.brand[500], light: tokens.brand[200] },
        background: { default: tokens.surface.dark.page, paper: tokens.surface.dark.card },
        text: {
          primary: tokens.ink.dark.primary,
          secondary: tokens.ink.dark.secondary,
          disabled: tokens.ink.dark.muted,
        },
      },
    },
  },
  shape: { borderRadius: tokens.radius.card },
  typography: {
    fontFamily: `"Manrope", -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif`,
    fontSize: 15,
    fontWeightLight: 300,
    fontWeightRegular: 400,
    fontWeightMedium: 600,
    fontWeightBold: 700,
  },
  breakpoints: { values: { xs: 0, sm: 600, md: 900, lg: 1200, xl: 2000 } },
});

export default theme;
```

- [ ] **Step 4: Run it to verify it passes**

Run: `cd web && npm test`
Expected: PASS.

- [ ] **Step 5: Wire it into App.tsx**

In `web/src/App.tsx`, delete the inline `createTheme` call and its `theme` const, remove the now-unused `createTheme` import, and import the theme instead:

```tsx
import theme from "./theme/theme";
```

Leave `ThemeProvider` and `CssBaseline` as they are, now receiving the imported theme.

- [ ] **Step 6: Verify the app still builds**

Run: `cd web && npm run build && npm run check:init`
Expected: both exit 0.

- [ ] **Step 7: Commit**

```bash
git add web/src/theme/theme.ts web/src/theme/theme.test.ts web/src/App.tsx
git commit -m "feat: build the mui theme from design tokens"
```

---

### Task 4: Typography ramp and Manrope

**Files:**
- Modify: `web/package.json`
- Modify: `web/src/index.css`
- Modify: `web/src/theme/theme.ts`
- Modify: `web/src/theme/theme.test.ts`

**Interfaces:**
- Consumes: `theme` from Task 3.
- Produces: named typography variants `heroNumber`, `pageTitle`, `sectionTitle`, `cardTitle`, `body1`, `body2`, `overline`, `numeric` on the theme.

- [ ] **Step 1: Swap the font package**

```bash
cd web && npm uninstall @fontsource/lato && npm install @fontsource/manrope@^5.3.0
```

- [ ] **Step 2: Replace the font imports**

In `web/src/index.css`, replace the three Lato imports currently in `App.tsx`'s import block. Move font loading here and delete the Lato lines from `web/src/App.tsx`:

```css
@import "@fontsource/manrope/300.css";
@import "@fontsource/manrope/400.css";
@import "@fontsource/manrope/600.css";
@import "@fontsource/manrope/700.css";
```

- [ ] **Step 3: Write the failing test**

Append to `web/src/theme/theme.test.ts`:

```ts
describe("typography ramp", () => {
  it("sizes the hero number at thirty-two pixels and bold", () => {
    expect(theme.typography.heroNumber.fontSize).toBe("2rem");
    expect(theme.typography.heroNumber.fontWeight).toBe(700);
  });

  it("gives the numeric variant tabular figures", () => {
    expect(theme.typography.numeric.fontVariantNumeric).toBe("tabular-nums");
  });

  it("does not give the hero number tabular figures", () => {
    expect(theme.typography.heroNumber.fontVariantNumeric).toBeUndefined();
  });

  it("sets body line height for reading", () => {
    expect(theme.typography.body1.lineHeight).toBe(1.55);
  });
});
```

- [ ] **Step 4: Run it to verify it fails**

Run: `cd web && npx vitest run src/theme/theme.test.ts`
Expected: FAIL — `heroNumber` is undefined.

- [ ] **Step 5: Declare the variants**

Create `web/src/theme/typography.d.ts`:

```ts
declare module "@mui/material/styles" {
  interface TypographyVariants {
    heroNumber: React.CSSProperties;
    numeric: React.CSSProperties;
  }
  interface TypographyVariantsOptions {
    heroNumber?: React.CSSProperties;
    numeric?: React.CSSProperties;
  }
}

declare module "@mui/material/Typography" {
  interface TypographyPropsVariantOverrides {
    heroNumber: true;
    numeric: true;
  }
}

export {};
```

In `web/src/theme/theme.ts`, extend the `typography` block:

```ts
    heroNumber: { fontSize: "2rem", fontWeight: 700, letterSpacing: "-0.02em", lineHeight: 1.1 },
    h1: { fontSize: "1.5rem", fontWeight: 700, letterSpacing: "-0.015em" },
    h2: { fontSize: "1.25rem", fontWeight: 600 },
    h3: { fontSize: "1rem", fontWeight: 700 },
    body1: { fontSize: "0.9375rem", fontWeight: 400, lineHeight: 1.55 },
    body2: { fontSize: "0.8125rem", fontWeight: 400 },
    overline: {
      fontSize: "0.6875rem",
      fontWeight: 700,
      letterSpacing: "0.09em",
      textTransform: "uppercase",
    },
    numeric: { fontSize: "0.9375rem", fontWeight: 600, fontVariantNumeric: "tabular-nums" },
```

- [ ] **Step 6: Run it to verify it passes**

Run: `cd web && npm test`
Expected: PASS.

- [ ] **Step 7: Verify no Lato remains**

Run: `cd web && grep -rn "lato" src package.json || echo "clean"`
Expected: `clean`.

- [ ] **Step 8: Commit**

```bash
git add web/package.json web/package-lock.json web/src/index.css web/src/App.tsx web/src/theme/
git commit -m "feat: replace lato with manrope and add the typography ramp"
```

---

### Task 5: Density tokens

**Files:**
- Create: `web/src/theme/density.ts`
- Create: `web/src/theme/density.test.ts`

**Interfaces:**
- Consumes: nothing.
- Produces: `density` object with `comfortable` and `compact`, each `{ rowHeight: number, bodySize: string, paddingX: number, paddingY: number }`. The transactions grid consumes `compact`; every other surface consumes `comfortable`.

- [ ] **Step 1: Write the failing test**

`web/src/theme/density.test.ts`:

```ts
import { describe, expect, it } from "vitest";
import { density } from "./density";

describe("density tokens", () => {
  it("makes comfortable rows tall enough for a touch target", () => {
    expect(density.comfortable.rowHeight).toBeGreaterThanOrEqual(44);
  });

  it("makes compact rows shorter than comfortable ones", () => {
    expect(density.compact.rowHeight).toBeLessThan(density.comfortable.rowHeight);
  });

  it("uses the fifteen pixel body size at comfortable density", () => {
    expect(density.comfortable.bodySize).toBe("0.9375rem");
  });

  it("drops to fourteen pixels at compact density", () => {
    expect(density.compact.bodySize).toBe("0.875rem");
  });
});
```

- [ ] **Step 2: Run it to verify it fails**

Run: `cd web && npx vitest run src/theme/density.test.ts`
Expected: FAIL — cannot resolve `./density`.

- [ ] **Step 3: Write the density tokens**

`web/src/theme/density.ts`:

```ts
export const density = {
  comfortable: { rowHeight: 56, bodySize: "0.9375rem", paddingX: 16, paddingY: 14 },
  compact: { rowHeight: 40, bodySize: "0.875rem", paddingX: 12, paddingY: 8 },
} as const;

export type DensityName = keyof typeof density;
```

- [ ] **Step 4: Run it to verify it passes**

Run: `cd web && npm test`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add web/src/theme/density.ts web/src/theme/density.test.ts
git commit -m "feat: add comfortable and compact density tokens"
```

---

### Task 6: The delta chip

**Files:**
- Create: `web/src/components/DeltaChip/DeltaChip.tsx`
- Create: `web/src/components/DeltaChip/DeltaChip.test.tsx`

**Interfaces:**
- Consumes: `tokens` from Task 2, `theme` from Task 3.
- Produces: `DeltaChip` accepting `{ direction: "up" | "down", tone: "overBudget" | "comparison", children: ReactNode }`.

- [ ] **Step 1: Write the failing test**

`web/src/components/DeltaChip/DeltaChip.test.tsx`:

```tsx
import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { ThemeProvider } from "@mui/material/styles";
import theme from "../../theme/theme";
import DeltaChip from "./DeltaChip";

const renderChip = (element: React.ReactElement) =>
  render(<ThemeProvider theme={theme}>{element}</ThemeProvider>);

describe("DeltaChip", () => {
  it("shows its label", () => {
    renderChip(
      <DeltaChip direction="up" tone="overBudget">
        8% over budget
      </DeltaChip>
    );
    expect(screen.getByText("8% over budget")).toBeDefined();
  });

  it("carries a direction arrow so colour is never the only signal", () => {
    renderChip(
      <DeltaChip direction="up" tone="overBudget">
        8% over budget
      </DeltaChip>
    );
    expect(screen.getByLabelText("increase")).toBeDefined();
  });

  it("labels a downward delta as a decrease", () => {
    renderChip(
      <DeltaChip direction="down" tone="comparison">
        12% vs last month
      </DeltaChip>
    );
    expect(screen.getByLabelText("decrease")).toBeDefined();
  });
});
```

- [ ] **Step 2: Run it to verify it fails**

Run: `cd web && npx vitest run src/components/DeltaChip/DeltaChip.test.tsx`
Expected: FAIL — cannot resolve `./DeltaChip`.

- [ ] **Step 3: Write the component**

`web/src/components/DeltaChip/DeltaChip.tsx`:

```tsx
import { ReactNode } from "react";
import Box from "@mui/material/Box";
import { ArrowDown, ArrowUp } from "lucide-react";
import { tokens } from "../../theme/tokens";

interface DeltaChipProps {
  direction: "up" | "down";
  tone: "overBudget" | "comparison";
  children: ReactNode;
}

const DeltaChip = ({ direction, tone, children }: DeltaChipProps) => {
  const Arrow = direction === "up" ? ArrowUp : ArrowDown;
  const arrowLabel = direction === "up" ? "increase" : "decrease";

  return (
    <Box
      component="span"
      sx={{
        display: "inline-flex",
        alignItems: "center",
        gap: 0.5,
        borderRadius: `${tokens.radius.pill}px`,
        paddingInline: 1.25,
        paddingBlock: 0.375,
        fontSize: "0.75rem",
        fontWeight: 700,
        color: tokens.chip.light[tone].text,
        backgroundColor: tokens.chip.light[tone].wash,
        "[data-theme='dark'] &": {
          color: tokens.chip.dark[tone].text,
          backgroundColor: tokens.chip.dark[tone].wash,
        },
      }}
    >
      <Arrow size={13} aria-label={arrowLabel} />
      {children}
    </Box>
  );
};

export default DeltaChip;
```

- [ ] **Step 4: Run it to verify it passes**

Run: `cd web && npm test`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add web/src/components/DeltaChip/
git commit -m "feat: add the delta chip with an arrow so colour never carries meaning alone"
```

---

### Task 7: Theme mode toggle

**Files:**
- Create: `web/src/components/ThemeModeToggle/ThemeModeToggle.tsx`
- Create: `web/src/components/ThemeModeToggle/ThemeModeToggle.test.tsx`
- Modify: `web/index.html`
- Modify: `web/src/App.tsx`

**Interfaces:**
- Consumes: `theme` from Task 3.
- Produces: `ThemeModeToggle`, a three-way control over MUI's `useColorScheme` with values `light`, `dark`, `system`.

- [ ] **Step 1: Write the failing test**

`web/src/components/ThemeModeToggle/ThemeModeToggle.test.tsx`:

```tsx
import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { ThemeProvider } from "@mui/material/styles";
import theme from "../../theme/theme";
import ThemeModeToggle from "./ThemeModeToggle";

describe("ThemeModeToggle", () => {
  it("offers light, dark and system", () => {
    render(
      <ThemeProvider theme={theme}>
        <ThemeModeToggle />
      </ThemeProvider>
    );
    expect(screen.getByRole("button", { name: "Light" })).toBeDefined();
    expect(screen.getByRole("button", { name: "Dark" })).toBeDefined();
    expect(screen.getByRole("button", { name: "System" })).toBeDefined();
  });
});
```

- [ ] **Step 2: Run it to verify it fails**

Run: `cd web && npx vitest run src/components/ThemeModeToggle/ThemeModeToggle.test.tsx`
Expected: FAIL — cannot resolve `./ThemeModeToggle`.

- [ ] **Step 3: Write the component**

`web/src/components/ThemeModeToggle/ThemeModeToggle.tsx`:

```tsx
import { useColorScheme } from "@mui/material/styles";
import ToggleButton from "@mui/material/ToggleButton";
import ToggleButtonGroup from "@mui/material/ToggleButtonGroup";

const modes = ["light", "dark", "system"] as const;

const labels: Record<(typeof modes)[number], string> = {
  light: "Light",
  dark: "Dark",
  system: "System",
};

const ThemeModeToggle = () => {
  const { mode, setMode } = useColorScheme();

  return (
    <ToggleButtonGroup
      size="small"
      exclusive
      value={mode ?? "system"}
      onChange={(_event, next) => next && setMode(next)}
    >
      {modes.map((option) => (
        <ToggleButton key={option} value={option} aria-label={labels[option]}>
          {labels[option]}
        </ToggleButton>
      ))}
    </ToggleButtonGroup>
  );
};

export default ThemeModeToggle;
```

- [ ] **Step 4: Prevent the flash of the wrong theme**

In `web/index.html`, add as the first child of `<body>`:

```html
<script>
  (function () {
    var stored = localStorage.getItem("mui-mode");
    var dark = stored === "dark" || (stored !== "light" && matchMedia("(prefers-color-scheme: dark)").matches);
    document.documentElement.setAttribute("data-theme", dark ? "dark" : "light");
  })();
</script>
```

- [ ] **Step 5: Run it to verify it passes**

Run: `cd web && npm test`
Expected: PASS.

- [ ] **Step 6: Mount the toggle**

In `web/src/App.tsx`, the toggle belongs on the Manage page once that exists. Until then, render it in `Settings.tsx` so the behaviour is reachable and testable by hand.

- [ ] **Step 7: Verify the build**

Run: `cd web && npm run build && npm run check:init`
Expected: both exit 0.

- [ ] **Step 8: Commit**

```bash
git add web/src/components/ThemeModeToggle/ web/index.html web/src/pages/Settings/Settings.tsx
git commit -m "feat: add a light dark system theme toggle"
```

---

### Task 8: Focus ring and reduced motion

The app has no visible focus ring today. This is the accessibility floor, not a nicety.

**Files:**
- Modify: `web/src/theme/theme.ts`
- Modify: `web/src/theme/theme.test.ts`

**Interfaces:**
- Consumes: `tokens` from Task 2.
- Produces: `components.MuiCssBaseline.styleOverrides` carrying the focus ring and the reduced-motion block.

- [ ] **Step 1: Write the failing test**

Append to `web/src/theme/theme.test.ts`:

```ts
describe("accessibility baseline", () => {
  const baseline = () => {
    const overrides = theme.components?.MuiCssBaseline?.styleOverrides;
    return typeof overrides === "string" ? overrides : JSON.stringify(overrides);
  };

  it("defines a visible focus ring", () => {
    expect(baseline()).toMatch(/focus-visible/);
  });

  it("honours prefers-reduced-motion", () => {
    expect(baseline()).toMatch(/prefers-reduced-motion/);
  });
});
```

- [ ] **Step 2: Run it to verify it fails**

Run: `cd web && npx vitest run src/theme/theme.test.ts`
Expected: FAIL — `components` is undefined.

- [ ] **Step 3: Add the overrides**

In `web/src/theme/theme.ts`, add a `components` block to the `createTheme` argument:

```ts
  components: {
    MuiCssBaseline: {
      styleOverrides: `
        :where(a, button, input, select, textarea, [tabindex]):focus-visible {
          outline: 2px solid ${tokens.brand[500]};
          outline-offset: 2px;
        }
        [data-theme='dark'] :where(a, button, input, select, textarea, [tabindex]):focus-visible {
          outline-color: ${tokens.brand[400]};
        }
        @media (prefers-reduced-motion: reduce) {
          *, *::before, *::after {
            animation-duration: 0.01ms !important;
            animation-iteration-count: 1 !important;
            transition-duration: 0.01ms !important;
            scroll-behavior: auto !important;
          }
        }
      `,
    },
  },
```

- [ ] **Step 4: Run the full suite**

Run: `cd web && npm test`
Expected: PASS, all tests.

- [ ] **Step 5: Verify the build and init check**

Run: `cd web && npm run build && npm run check:init`
Expected: both exit 0.

- [ ] **Step 6: Commit**

```bash
git add web/src/theme/theme.ts web/src/theme/theme.test.ts
git commit -m "feat: add a visible focus ring and honour reduced motion"
```

---

## Self-review

**Spec coverage.** Character (soft cards) lands as `radius` tokens and `shape.borderRadius` in Tasks 2 and 3. Manrope and the 15px base in Task 4. Both colour schemes in Task 3. Brand ramp, category palette, priority ramp, money colours and chip colours in Task 2, each with an assertion. Density in Task 5. Delta chip in Task 6. Toggle in Task 7. Motion tokens in Task 2, applied with the focus ring in Task 8.

**Not covered by this plan, deliberately.** Elevation levels 1–3 are declared in the spec but only consumed once real cards exist; they belong to the IA plan that builds those cards, and adding unused shadow overrides now would be speculative. Mobile card-list behaviour for the transactions grid likewise belongs to the IA plan, since there is no grid to switch yet. The eight chart rules are constraints on chart code that does not exist yet.

**Type consistency.** `tokens` is the single exported name from `tokens.ts` and is imported identically in Tasks 3, 6 and 8. `density` is a separate export used by no task here, which is correct — it is consumed by the IA plan. `contrastRatio(foreground, background)` keeps that signature in Tasks 1, 2 and 6. `theme` is a default export throughout.

**One risk to flag for the executor.** Task 6 styles the dark variant with a `[data-theme='dark'] &` selector, which depends on Task 3 setting `colorSchemeSelector: "data-theme"`. If that selector changes, the chip's dark colours silently stop applying — the test asserts contrast of the token values, not of the rendered element, so it would not catch it. If this becomes fragile, assert computed styles instead.
