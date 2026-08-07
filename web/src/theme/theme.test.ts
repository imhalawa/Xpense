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
