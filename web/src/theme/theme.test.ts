import { describe, expect, it } from "vitest";
import theme from "./theme";
import { tokens } from "./tokens";

describe("theme", () => {
  it("declares both colour schemes", () => {
    expect(theme.colorSchemes.light).toBeDefined();
    expect(theme.colorSchemes.dark).toBeDefined();
  });

  it("uses the brand blue as light primary and the lighter step as dark primary", () => {
    expect(theme.colorSchemes.light!.palette.primary.main).toBe(tokens.brand[500]);
    expect(theme.colorSchemes.dark!.palette.primary.main).toBe(tokens.brand[400]);
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
