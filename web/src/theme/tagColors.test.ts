import { describe, expect, it } from "vitest";
import { contrastRatio } from "./contrast";
import { categoryPaletteSlot, categorySlotCount, neutralTagColors, resolveTagColors } from "./tagColors";

const highContrastForeground = "#595959";
const highContrastBackground = "#ffffff";
const lowContrastForeground = "#cccccc";
const lowContrastBackground = "#ffffff";

describe("resolveTagColors", () => {
  it("keeps a stored pair that clears seven to one", () => {
    expect(contrastRatio(highContrastForeground, highContrastBackground)).toBeGreaterThanOrEqual(7);
    expect(resolveTagColors(highContrastForeground, highContrastBackground)).toEqual({
      foreground: highContrastForeground,
      background: highContrastBackground,
    });
  });

  it("keeps a stored pair sitting exactly on the four point five boundary", () => {
    const boundaryForeground = "#767676";
    expect(contrastRatio(boundaryForeground, highContrastBackground)).toBeGreaterThanOrEqual(4.5);
    expect(resolveTagColors(boundaryForeground, highContrastBackground)).toEqual({
      foreground: boundaryForeground,
      background: highContrastBackground,
    });
  });

  it("falls back to the neutral badge pair below four point five", () => {
    expect(contrastRatio(lowContrastForeground, lowContrastBackground)).toBeLessThan(4.5);
    expect(resolveTagColors(lowContrastForeground, lowContrastBackground)).toEqual(neutralTagColors);
  });

  it("falls back when either half of the pair is missing", () => {
    expect(resolveTagColors(null, highContrastBackground)).toEqual(neutralTagColors);
    expect(resolveTagColors(highContrastForeground, null)).toEqual(neutralTagColors);
    expect(resolveTagColors(null, null)).toEqual(neutralTagColors);
  });

  it("falls back on a value that is not a six digit hex instead of passing it through", () => {
    expect(resolveTagColors("rebeccapurple", highContrastBackground)).toEqual(neutralTagColors);
    expect(resolveTagColors("#fff", "#000")).toEqual(neutralTagColors);
    expect(resolveTagColors("#zzzzzz", highContrastBackground)).toEqual(neutralTagColors);
    expect(resolveTagColors("", "")).toEqual(neutralTagColors);
  });

  it("resolves the neutral pair to Fluent tokens rather than hex values", () => {
    expect(neutralTagColors.foreground).toContain("var(--");
    expect(neutralTagColors.background).toContain("var(--");
  });
});

describe("categoryPaletteSlot", () => {
  it("returns the same slot for the same identifier across calls", () => {
    const identifier = "5f6d2a1c-9b40-4c2e-8f77-1a2b3c4d5e6f";
    expect(categoryPaletteSlot(identifier)).toBe(categoryPaletteSlot(identifier));
  });

  it("stays inside the eight slot palette for numeric and uuid shaped identifiers", () => {
    const identifiers = Array.from({ length: 200 }, (_unused, position) => `category-${position}`);
    for (const identifier of identifiers) {
      const slot = categoryPaletteSlot(identifier);
      expect(Number.isInteger(slot)).toBe(true);
      expect(slot).toBeGreaterThanOrEqual(0);
      expect(slot).toBeLessThan(categorySlotCount);
    }
  });

  it("lets two different identifiers share a slot without throwing", () => {
    const identifiers = Array.from({ length: 200 }, (_unused, position) => `category-${position}`);
    const slots = identifiers.map(categoryPaletteSlot);
    expect(new Set(slots).size).toBe(categorySlotCount);
    expect(slots.length).toBeGreaterThan(new Set(slots).size);
  });

  it("gives an empty identifier a slot rather than throwing", () => {
    expect(categoryPaletteSlot("")).toBeGreaterThanOrEqual(0);
    expect(categoryPaletteSlot("")).toBeLessThan(categorySlotCount);
  });

  it("matches the number of palette slots the tokens define", () => {
    expect(categorySlotCount).toBe(8);
  });
});
