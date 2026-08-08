import { describe, expect, it } from "vitest";
import { contrastRatio } from "../theme/contrast";
import { brandRamp } from "./brand";

const steps = [10, 20, 30, 40, 50, 60, 70, 80, 90, 100, 110, 120, 130, 140, 150, 160] as const;

describe("brandRamp", () => {
  it("has all sixteen Fluent steps", () => {
    steps.forEach((step) => expect(brandRamp[step]).toMatch(/^#[0-9a-fA-F]{6}$/));
  });

  it("contains the brand colour exactly", () => {
    expect(Object.values(brandRamp).map((hex) => hex.toLowerCase())).toContain("#1565c0");
  });

  it("gets lighter as the step number rises", () => {
    const ratios = steps.map((step) => contrastRatio(brandRamp[step], "#ffffff"));
    const descending = [...ratios].sort((first, second) => second - first);
    expect(ratios).toEqual(descending);
  });

  it("keeps the accent readable on a white surface", () => {
    expect(contrastRatio(brandRamp[80], "#ffffff")).toBeGreaterThanOrEqual(4.5);
  });
});
