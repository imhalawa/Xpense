import { describe, expect, it } from "vitest";
import { darkTheme, lightTheme } from "./theme";

describe("themes", () => {
  it("builds a light and a dark theme", () => {
    expect(lightTheme.colorBrandBackground).toBeDefined();
    expect(darkTheme.colorBrandBackground).toBeDefined();
  });

  it("gives them different neutral surfaces", () => {
    expect(lightTheme.colorNeutralBackground1).not.toBe(darkTheme.colorNeutralBackground1);
  });
});
