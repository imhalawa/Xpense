import { tokens as fluentTokens } from "@fluentui/react-components";
import { contrastRatio } from "./contrast";
import { tokens as xpenseTokens } from "./tokens";

const minimumTextContrastRatio = 4.5;
const sixDigitHexPattern = /^#[0-9a-fA-F]{6}$/;
const fowlerNollVoOffsetBasis = 2166136261;
const fowlerNollVoPrime = 16777619;

export const categorySlotCount = xpenseTokens.category.light.length;

export interface TagColors {
  foreground: string;
  background: string;
}

export const neutralTagColors: TagColors = {
  foreground: fluentTokens.colorNeutralForeground3,
  background: fluentTokens.colorNeutralBackground5,
};

export const resolveTagColors = (
  foregroundHex: string | null,
  backgroundHex: string | null
): TagColors => {
  if (foregroundHex === null || backgroundHex === null) {
    return neutralTagColors;
  }
  if (!sixDigitHexPattern.test(foregroundHex) || !sixDigitHexPattern.test(backgroundHex)) {
    return neutralTagColors;
  }
  if (contrastRatio(foregroundHex, backgroundHex) < minimumTextContrastRatio) {
    return neutralTagColors;
  }
  return { foreground: foregroundHex, background: backgroundHex };
};

export const categoryPaletteSlot = (categoryId: string): number => {
  let hash = fowlerNollVoOffsetBasis;
  for (let position = 0; position < categoryId.length; position += 1) {
    hash ^= categoryId.charCodeAt(position);
    hash = Math.imul(hash, fowlerNollVoPrime);
  }
  return (hash >>> 0) % categorySlotCount;
};
