import { createTheme } from "@mui/material";

/**
 * Construye el tema MUI para el modo dado. `primary.main` se mantiene fijo en ambos modos
 * (`#16213e`, ratio de contraste ~14.8:1 con texto blanco, verificado en UX-UI-Audit.md GL-04)
 * para no arriesgar el contraste del AppBar; solo cambian las superficies de fondo — ver
 * Design-System.md "Modo oscuro" para la justificación completa (Material 3 / Fluent 2 / HIG
 * tratan el modo oscuro como expectativa base, no como extra).
 */
export function buildTheme(mode: "light" | "dark") {
  return createTheme({
    palette: {
      mode,
      primary: { main: "#16213e" },
      secondary: { main: "#3f51b5" },
      background:
        mode === "dark"
          ? { default: "#121218", paper: "#1c1c24" }
          : { default: "#f4f6fb" },
    },
    typography: { fontFamily: '"Segoe UI", Roboto, Helvetica, Arial, sans-serif' },
    shape: { borderRadius: 8 },
  });
}
