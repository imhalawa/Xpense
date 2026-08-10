import { describe, expect, it } from "vitest";
import { contrastRatio } from "./contrast";
import { resolveTagColors } from "./tagColors";
import { matchingPreset, tagPresetChoices, tagPresets } from "./tagPresets";

const minimumTextContrastRatio = 4.5;
const sixDigitHex = /^#[0-9a-f]{6}$/;

describe("tag presets", () => {
  it("offers one choice per category slot plus a neutral", () => {
    expect(tagPresets).toHaveLength(8);
    expect(tagPresetChoices).toHaveLength(9);
    expect(tagPresetChoices[0]?.name).toBe("Neutral");
  });

  it("keeps every preset readable", () => {
    for (const preset of tagPresetChoices) {
      expect(preset.backgroundHex).toMatch(sixDigitHex);
      expect(preset.foregroundHex).toMatch(sixDigitHex);
      expect(contrastRatio(preset.foregroundHex, preset.backgroundHex))
        .toBeGreaterThanOrEqual(minimumTextContrastRatio);
    }
  });

  it("survives the chip colour resolver rather than falling back to neutral", () => {
    for (const preset of tagPresetChoices) {
      expect(resolveTagColors(preset.foregroundHex, preset.backgroundHex)).toEqual({
        foreground: preset.foregroundHex,
        background: preset.backgroundHex,
      });
    }
  });

  it("recognises a stored pair as one of its own presets", () => {
    const chosen = tagPresetChoices[3]!;
    expect(matchingPreset(chosen.foregroundHex, chosen.backgroundHex)).toEqual(chosen);
    expect(matchingPreset("#123456", "#654321")).toBeNull();
    expect(matchingPreset(null, null)).toBeNull();
  });
});
