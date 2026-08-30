/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    "./app/**/*.{js,ts,jsx,tsx}",
    "./components/**/*.{js,ts,jsx,tsx}",
    "./lib/**/*.{js,ts,jsx,tsx}"
  ],
  theme: {
    extend: {
      colors: {
        primary: "#2563eb",
        primaryHover: "#1d4ed8",
        accent: "#10b981",
        accentHover: "#059669",
        surface: "#f8fafc",
        surfaceDark: "#0f172a",
      },
      fontFamily: {
        sans: ["Inter", "ui-sans", "system-ui", "sans-serif"],
        display: ["Inter", "ui-sans", "system-ui", "sans-serif"],
      },
      boxShadow: {
        card: "0 10px 32px rgb(0 0 0 / 0.06), 0 1px 2px rgb(0 0 0 / 0.04)",
        cardDark: "0 10px 32px rgb(0 0 0 / 0.30)",
      },
      backgroundImage: {
        heroGradient: "linear-gradient(135deg, #2563eb 0%, #0d9488 55%, #10b981 100%)",
      },
      container: {
        center: true,
        padding: "1rem",
      },
    },
  },
  plugins: [require("@tailwindcss/typography")],
};
