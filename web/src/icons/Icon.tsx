import { ReactNode } from "react";

export interface IconProps {
  size?: number;
  title?: string;
  className?: string;
}

interface BaseIconProps extends IconProps {
  children: ReactNode;
}

const Icon = ({ size = 20, title, className, children }: BaseIconProps) => (
  <svg
    width={size}
    height={size}
    viewBox="0 0 24 24"
    fill="none"
    stroke="currentColor"
    strokeWidth={1.75}
    strokeLinecap="round"
    strokeLinejoin="round"
    className={className}
    role={title ? "img" : undefined}
    aria-label={title}
    aria-hidden={title ? undefined : true}
    focusable="false">
    {title && <title>{title}</title>}
    {children}
  </svg>
);

export default Icon;
