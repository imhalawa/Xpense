import { describe, expect, it } from "vitest";
import { darkTheme, duration, easing, lightTheme, radius } from "./theme";

describe("themes", () => {
  it("builds a light and a dark theme", () => {
    expect(lightTheme.colorBrandBackground).toBeDefined();
    expect(darkTheme.colorBrandBackground).toBeDefined();
  });

  it("gives them different neutral surfaces", () => {
    expect(lightTheme.colorNeutralBackground1).not.toBe(darkTheme.colorNeutralBackground1);
  });

  it("sets Manrope and the 15px body from the design standard", () => {
    for (const theme of [lightTheme, darkTheme]) {
      expect(theme.fontFamilyBase).toContain("Manrope");
      expect(theme.fontSizeBase300).toBe("15px");
    }
  });

  it("rounds controls and cards to the documented radii", () => {
    for (const theme of [lightTheme, darkTheme]) {
      expect(theme.borderRadiusMedium).toBe(radius.control);
      expect(theme.borderRadiusLarge).toBe(radius.card);
      expect(theme.borderRadiusXLarge).toBe(radius.card);
    }
  });

  it("uses the documented motion vocabulary", () => {
    expect(duration).toEqual({ fast: "120ms", base: "200ms", slow: "320ms" });
    expect(lightTheme.durationNormal).toBe(duration.base);
    expect(lightTheme.curveEasyEase).toBe(easing.entering);
    expect(lightTheme.curveAccelerateMid).toBe(easing.leaving);
  });
});
