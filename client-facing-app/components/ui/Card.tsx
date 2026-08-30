import type { HTMLAttributes } from "react";

export interface CardProps extends HTMLAttributes<HTMLDivElement> {
  elevated?: boolean;
}

export default function Card({
  className = "",
  elevated = true,
  children,
  ...props
}: CardProps) {
  const classes = elevated
    ? `rounded-2xl bg-white p-6 shadow-card ring-1 ring-slate-200/60 ${className}`
    : `rounded-2xl bg-white/70 p-6 ${className}`;
  return (
    <div className={classes} {...props}>
      {children}
    </div>
  );
}
