export const theme = {
  colors: {
    primary: "#2563eb",
    primaryHover: "#1d4ed8",
    accent: "#10b981",
    accentHover: "#059669",
    surface: "#f8fafc",
    surfaceDark: "#0f172a",
    text: "#0f172a",
    muted: "#64748b",
    border: "#e2e8f0",
  },
  fonts: {
    sans: "Inter, ui-sans, system-ui, sans-serif",
    display: "Inter, ui-sans, system-ui, sans-serif",
  },
  radius: {
    card: "1rem",
  },
  shadow: {
    card: "0 10px 32px rgb(0 0 0 / 0.06), 0 1px 2px rgb(0 0 0 / 0.04)",
  },
} as const;

export type Theme = typeof theme;
