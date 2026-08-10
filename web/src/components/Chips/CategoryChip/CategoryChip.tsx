import { makeStyles, mergeClasses, tokens } from "@fluentui/react-components";
import type { TagColors } from "../../../theme/tagColors";
import { categoryPaletteSlot, neutralTagColors, resolveTagColors } from "../../../theme/tagColors";
import { neutralPreset, tagPresets } from "../../../theme/tagPresets";

export interface CategoryChipProps {
  label: string;
  categoryId?: string;
  foregroundHex?: string | null;
  backgroundHex?: string | null;
  onClick?: () => void;
}

const dotSize = "8px";

export const categoryChipColors = ({
  categoryId,
  foregroundHex,
  backgroundHex,
}: Pick<CategoryChipProps, "categoryId" | "foregroundHex" | "backgroundHex">): TagColors => {
  if (typeof foregroundHex === "string" && typeof backgroundHex === "string") {
    return resolveTagColors(foregroundHex, backgroundHex);
  }
  if (categoryId === undefined) {
    return neutralTagColors;
  }
  const preset = tagPresets[categoryPaletteSlot(categoryId)] ?? neutralPreset;
  return resolveTagColors(preset.foregroundHex, preset.backgroundHex);
};

const useStyles = makeStyles({
  chip: {
    display: "inline-flex",
    alignItems: "center",
    gap: tokens.spacingHorizontalXS,
    maxWidth: "100%",
    minHeight: "24px",
    paddingInline: tokens.spacingHorizontalS,
    paddingBlock: tokens.spacingVerticalXXS,
    borderRadius: tokens.borderRadiusCircular,
    fontFamily: tokens.fontFamilyBase,
    fontSize: tokens.fontSizeBase200,
    fontWeight: tokens.fontWeightSemibold,
    lineHeight: tokens.lineHeightBase200,
    textAlign: "start",
  },
  interactive: {
    borderTopStyle: "none",
    borderRightStyle: "none",
    borderBottomStyle: "none",
    borderLeftStyle: "none",
    cursor: "pointer",
    transitionProperty: "box-shadow, transform",
    transitionDuration: tokens.durationFast,
    transitionTimingFunction: tokens.curveDecelerateMax,
    ":hover": {
      boxShadow: `inset 0 0 0 ${tokens.strokeWidthThin} currentColor`,
      transform: "translateY(-1px)",
    },
    ":active": {
      transform: "none",
    },
    ":focus-visible": {
      outlineWidth: tokens.strokeWidthThick,
      outlineStyle: "solid",
      outlineColor: tokens.colorBrandStroke1,
      outlineOffset: tokens.strokeWidthThick,
      borderRadius: tokens.borderRadiusCircular,
    },
  },
  dot: {
    flexShrink: 0,
    width: dotSize,
    height: dotSize,
    borderRadius: tokens.borderRadiusCircular,
    backgroundColor: "currentColor",
  },
  label: {
    minWidth: 0,
    overflow: "hidden",
    textOverflow: "ellipsis",
    whiteSpace: "nowrap",
  },
});

const CategoryChip = ({ label, categoryId, foregroundHex, backgroundHex, onClick }: CategoryChipProps) => {
  const styles = useStyles();
  const colors = categoryChipColors({ categoryId, foregroundHex, backgroundHex });
  const paint = { color: colors.foreground, backgroundColor: colors.background };
  const content = (
    <>
      <span className={styles.dot} aria-hidden />
      <span className={styles.label}>{label}</span>
    </>
  );

  if (onClick === undefined) {
    return (
      <span className={styles.chip} style={paint}>
        {content}
      </span>
    );
  }

  return (
    <button
      type="button"
      className={mergeClasses(styles.chip, styles.interactive)}
      style={paint}
      onClick={onClick}>
      {content}
    </button>
  );
};

export default CategoryChip;
