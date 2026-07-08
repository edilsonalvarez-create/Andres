/** @type {import('tailwindcss').Config} */
export default {
  content: ["./index.html", "./src/**/*.{ts,tsx}"],
  important: true,
  theme: {
    extend: {
      colors: {
        guardian: {
          900: "#16213e",
          700: "#1a2b5e",
          500: "#3f51b5",
          100: "#e8eaf6",
        },
      },
    },
  },
  plugins: [],
};
