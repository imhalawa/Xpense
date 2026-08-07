import type React from "react";

declare module "@mui/material/styles" {
  interface TypographyVariants {
    heroNumber: React.CSSProperties;
    numeric: React.CSSProperties;
  }
  interface TypographyVariantsOptions {
    heroNumber?: React.CSSProperties;
    numeric?: React.CSSProperties;
  }
}

declare module "@mui/material/Typography" {
  interface TypographyPropsVariantOverrides {
    heroNumber: true;
    numeric: true;
  }
}

export {};
