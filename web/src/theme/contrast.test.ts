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
