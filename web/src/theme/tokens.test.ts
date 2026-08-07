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
