import { makeStyles, mergeClasses, tokens } from "@fluentui/react-components";

const useStyles = makeStyles({
  root: {
    display: "inline-flex",
    alignItems: "center",
    gap: tokens.spacingHorizontalSNudge,
    color: tokens.colorNeutralForeground1,
  },
  mark: { flexShrink: 0 },
  name: {
    fontFamily: tokens.fontFamilyBase,
    fontSize: tokens.fontSizeBase600,
    fontWeight: tokens.fontWeightBold,
    letterSpacing: "-0.02em",
    lineHeight: tokens.lineHeightBase600,
  },
});

export interface WordmarkProps {
  className?: string;
  showName?: boolean;
  size?: number;
}

export const Wordmark = ({ className, showName = true, size = 28 }: WordmarkProps) => {
  const styles = useStyles();
  return (
    <span className={mergeClasses(styles.root, className)}>
      <svg
        className={styles.mark}
        width={size}
        height={size}
        viewBox="0 0 32 32"
        role="img"
        aria-label="Xpense">
        <rect width="32" height="32" rx="9" fill={tokens.colorBrandBackground} />
        <path
          d="M9 9.5 L23 22.5"
          stroke={tokens.colorNeutralForegroundOnBrand}
          strokeWidth="3.2"
          strokeLinecap="round"
        />
        <path
          d="M23 9.5 L9 22.5"
          stroke={tokens.colorNeutralForegroundOnBrand}
          strokeWidth="3.2"
          strokeLinecap="round"
          opacity="0.55"
        />
      </svg>
      {showName && <span className={styles.name}>Xpense</span>}
    </span>
  );
};

export default Wordmark;
