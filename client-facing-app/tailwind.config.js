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
        slate: { 50: '#fffaf5', 100: '#fff2e7', 200: '#f6e1d0', 300: '#e6cbb8', 400: '#c4a99a', 500: '#a5897c', 600: '#795f56', 700: '#574239', 800: '#352925', 900: '#221b19', 950: '#171210' },
        blue: { 50: '#fff5ee', 100: '#ffe6d5', 200: '#ffcdb0', 300: '#ffb48b', 400: '#ffac82', 500: '#c65838', 600: '#b8482c', 700: '#96391f', 800: '#793420', 900: '#512c22', 950: '#321d16' },
        primary: "#b8482c",
        primaryHover: "#96391f",
        accent: "#f4bc91",
        accentHover: "#059669",
        surface: "#f8fafc",
        surfaceDark: "#0f172a",
      },
      fontFamily: {
        sans: ["Trebuchet MS", "Avenir Next", "ui-sans", "system-ui", "sans-serif"],
        display: ["Georgia", "Cambria", "serif"],
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
