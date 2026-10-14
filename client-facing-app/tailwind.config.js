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
        slate: { 50: '#f8fafb', 100: '#172e3b', 200: '#314854', 300: '#52636e', 400: '#657884', 500: '#84959e', 600: '#b8c5ca', 700: '#d9e2e5', 800: '#f0f4f5', 900: '#ffffff', 950: '#f8fafb' },
        blue: { 50: '#edf7f5', 100: '#d7eeea', 200: '#afd9d1', 300: '#237d72', 400: '#176b61', 500: '#197c70', 600: '#12685e', 700: '#0c5149', 800: '#103e3a', 900: '#14302e', 950: '#0d211f' },
        primary: "#12685e",
        primaryHover: "#0c5149",
        accent: "#f4bc91",
        accentHover: "#059669",
        surface: "#f8fafc",
        surfaceDark: "#0f172a",
      },
      fontFamily: {
        sans: ["Avenir Next", "Segoe UI", "ui-sans", "system-ui", "sans-serif"],
        display: ["Avenir Next", "Segoe UI", "Arial", "sans-serif"],
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
