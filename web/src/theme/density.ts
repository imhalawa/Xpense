export const density = {
  comfortable: { rowHeight: 56, bodySize: "0.9375rem", paddingX: 16, paddingY: 14 },
  compact: { rowHeight: 40, bodySize: "0.875rem", paddingX: 12, paddingY: 8 },
} as const;

export type DensityName = keyof typeof density;
