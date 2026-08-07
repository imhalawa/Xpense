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
