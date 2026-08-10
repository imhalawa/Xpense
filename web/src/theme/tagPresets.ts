import { contrastRatio } from "./contrast";
import { tokens } from "./tokens";

export interface TagPreset {
  name: string;
  backgroundHex: string;
  foregroundHex: string;
}

const presetNames = [
  "Blue",
  "Orange",
  "Aqua",
  "Yellow",
  "Magenta",
  "Green",
  "Violet",
  "Red",
] as const;

const white = { red: 255, green: 255, blue: 255 };
const ink = { red: 26, green: 34, blue: 48 };
const tintWeight = 0.88;
const minimumTextContrastRatio = 4.5;
const deepenStep = 0.05;
const maximumDeepen = 0.85;
const twoDigits = 2;

const channels = (hex: string) => ({
  red: Number.parseInt(hex.slice(1, 3), 16),
  green: Number.parseInt(hex.slice(3, 5), 16),
  blue: Number.parseInt(hex.slice(5, 7), 16),
});

const toHex = (value: number): string =>
  Math.round(Math.min(255, Math.max(0, value))).toString(16).padStart(twoDigits, "0");

const mix = (hex: string, towards: { red: number; green: number; blue: number }, weight: number) => {
  const source = channels(hex);
  return `#${toHex(source.red + (towards.red - source.red) * weight)}${
    toHex(source.green + (towards.green - source.green) * weight)}${
    toHex(source.blue + (towards.blue - source.blue) * weight)}`;
};

const readableInk = (hue: string, background: string): string => {
  let deepened = hue;
  for (let weight = 0; weight <= maximumDeepen; weight += deepenStep) {
    deepened = mix(hue, ink, weight);
    if (contrastRatio(deepened, background) >= minimumTextContrastRatio) return deepened;
  }
  return deepened;
};

export const tagPresets: readonly TagPreset[] = tokens.category.light.map((hue, index) => {
  const background = mix(hue, white, tintWeight);
  return {
    name: presetNames[index] ?? `Slot ${index + 1}`,
    backgroundHex: background,
    foregroundHex: readableInk(hue, background),
  };
});

export const neutralPreset: TagPreset = {
  name: "Neutral",
  backgroundHex: "#ededed",
  foregroundHex: "#242424",
};

export const tagPresetChoices: readonly TagPreset[] = [neutralPreset, ...tagPresets];

export const matchingPreset = (
  foregroundHex: string | null,
  backgroundHex: string | null,
): TagPreset | null =>
  tagPresetChoices.find((preset) =>
    preset.foregroundHex.toLowerCase() === foregroundHex?.toLowerCase() &&
    preset.backgroundHex.toLowerCase() === backgroundHex?.toLowerCase()) ?? null;
