import { describe, expect, it } from "vitest";
import { contrastRatio } from "./contrast";
import { tokens } from "./tokens";

describe("money tokens", () => {
  const cases: [string, string, string][] = [
    ["expense ordinary light", tokens.money.light.expenseOrdinary, tokens.surface.light.card],
    ["expense ordinary dark", tokens.money.dark.expenseOrdinary, tokens.surface.dark.card],
    ["expense alert light", tokens.money.light.expenseAlert, tokens.surface.light.card],
    ["expense alert dark", tokens.money.dark.expenseAlert, tokens.surface.dark.card],
    ["income light", tokens.money.light.income, tokens.surface.light.card],
    ["income dark", tokens.money.dark.income, tokens.surface.dark.card],
  ];

  it.each(cases)("%s clears AA body text", (_name, foreground, background) => {
    expect(contrastRatio(foreground, background)).toBeGreaterThanOrEqual(4.5);
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
