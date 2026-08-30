import type { ButtonHTMLAttributes, ReactNode } from "react";

export type ButtonVariant = "primary" | "accent" | "outline" | "ghost";
export type ButtonSize = "sm" | "md" | "lg";

export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant;
  size?: ButtonSize;
  icon?: ReactNode;
}

const base =
  "inline-flex items-center justify-center rounded-xl font-semibold transition-colors transition-shadow focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 disabled:opacity-60 disabled:cursor-not-allowed";

const variants: Record<ButtonVariant, string> = {
  primary:
    "bg-blue-600 text-white hover:bg-blue-700 hover:shadow-lg hover:shadow-blue-600/20 focus-visible:outline-blue-600",
  accent:
    "bg-emerald-500 text-slate-900 hover:bg-emerald-400 hover:shadow-lg hover:shadow-emerald-500/20 focus-visible:outline-emerald-500",
  outline:
    "border border-slate-300 text-slate-700 hover:bg-slate-100 focus-visible:outline-slate-400",
  ghost: "text-slate-600 hover:bg-slate-100 hover:text-slate-900 focus-visible:outline-slate-400",
};

const sizes: Record<ButtonSize, string> = {
  sm: "px-4 py-2 text-sm",
  md: "px-5 py-2.5",
  lg: "px-7 py-3 text-base",
};

export default function Button({
  variant = "primary",
  size = "md",
  icon,
  className = "",
  children,
  ...props
}: ButtonProps) {
  const classes = `${base} ${variants[variant]} ${sizes[size]} ${className}`;
  return (
    <button className={classes} {...props}>
      {icon ? <span className="mr-2 flex items-center">{icon}</span> : null}
      {children}
    </button>
  );
}
