import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import CategoryChip, { categoryChipColors } from "./CategoryChip";
import { contrastRatio } from "../../../theme/contrast";
import { categoryPaletteSlot, neutralTagColors } from "../../../theme/tagColors";
import { tagPresets } from "../../../theme/tagPresets";

const minimumTextContrastRatio = 4.5;
const hexChannel = (hex: string, start: number) => parseInt(hex.slice(start, start + 2), 16);
const rgb = (hex: string) => `rgb(${hexChannel(hex, 1)}, ${hexChannel(hex, 3)}, ${hexChannel(hex, 5)})`;

describe("CategoryChip", () => {
  it("shows its label", () => {
    render(<CategoryChip label="Groceries" categoryId="groceries" />);
    expect(screen.getByText("Groceries")).toBeDefined();
  });

  it("paints a category with its palette slot preset", () => {
    const preset = tagPresets[categoryPaletteSlot("groceries")];
    render(<CategoryChip label="Groceries" categoryId="groceries" />);
    const chip = screen.getByText("Groceries").parentElement;
    expect(chip?.style.color).toBe(rgb(preset.foregroundHex));
    expect(chip?.style.backgroundColor).toBe(rgb(preset.backgroundHex));
  });

  it("paints a tag with its own readable pair", () => {
    render(<CategoryChip label="Holiday" foregroundHex="#124e96" backgroundHex="#e8f1fb" />);
    const chip = screen.getByText("Holiday").parentElement;
    expect(chip?.style.color).toBe(rgb("#124e96"));
    expect(chip?.style.backgroundColor).toBe(rgb("#e8f1fb"));
  });

  it("gives every category slot a pair that clears AA body text", () => {
    tagPresets.forEach((preset, slot) => {
      const colors = categoryChipColors({ categoryId: `slot-${slot}`, foregroundHex: null, backgroundHex: null });
      expect(contrastRatio(colors.foreground, colors.background)).toBeGreaterThanOrEqual(
        minimumTextContrastRatio
      );
      expect(contrastRatio(preset.foregroundHex, preset.backgroundHex)).toBeGreaterThanOrEqual(
        minimumTextContrastRatio
      );
    });
  });

  it("falls back to neutral rather than painting a pair below 4.5:1", () => {
    expect(categoryChipColors({ foregroundHex: "#bbbbbb", backgroundHex: "#cccccc" })).toEqual(
      neutralTagColors
    );
    expect(
      categoryChipColors({ categoryId: "groceries", foregroundHex: "#bbbbbb", backgroundHex: "#cccccc" })
    ).toEqual(neutralTagColors);
    expect(categoryChipColors({})).toEqual(neutralTagColors);
  });

  it("stays a plain element with no affordance when it does nothing", () => {
    render(<CategoryChip label="Groceries" categoryId="groceries" />);
    expect(screen.queryByRole("button")).toBeNull();
  });

  it("becomes a button when it can be pressed", () => {
    const onClick = vi.fn();
    render(<CategoryChip label="Groceries" categoryId="groceries" onClick={onClick} />);
    screen.getByRole("button", { name: "Groceries" }).click();
    expect(onClick).toHaveBeenCalledOnce();
  });
});
