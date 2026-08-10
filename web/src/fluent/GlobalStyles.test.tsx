import { describe, expect, it } from "vitest";
import { tokens } from "../theme/tokens";
import { appGlobalStyles } from "./GlobalStyles";

const serializedStyles = JSON.stringify(appGlobalStyles);

describe("GlobalStyles", () => {
  it("provides a visible focus indicator", () => {
    expect(serializedStyles).toContain("outline");
    expect(serializedStyles).toContain("focus-visible");
  });

  it("honours reduced motion", () => {
    expect(serializedStyles).toContain("prefers-reduced-motion");
  });

  it("keeps portal layers transparent", () => {
    expect(appGlobalStyles).toEqual(
      expect.arrayContaining([
        expect.objectContaining({
          '[data-portal-node="true"]': expect.objectContaining({
            backgroundColor: "transparent",
          }),
        }),
      ])
    );
  });

  it("exposes the validated finance palette to rendered components", () => {
    expect(serializedStyles).toContain(tokens.money.light.income);
    expect(serializedStyles).toContain(tokens.money.dark.expenseAlert);
    expect(serializedStyles).toContain(tokens.category.light[3]);
    expect(serializedStyles).toContain(tokens.category.dark[6]);
  });
});
