import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from "react";
import { CssBaseline, ThemeProvider } from "@mui/material";
import { buildTheme } from "./theme";

type ThemeModePreference = "light" | "dark" | "system";
const STORAGE_KEY = "qaguardian.theme-mode";

interface ThemeModeContextValue {
  /** Preferencia elegida por el usuario (incluye "system"). */
  preference: ThemeModePreference;
  /** Modo real aplicado (resuelto contra el sistema si la preferencia es "system"). */
  effectiveMode: "light" | "dark";
  setPreference: (p: ThemeModePreference) => void;
}

const ThemeModeContext = createContext<ThemeModeContextValue | undefined>(undefined);

function getSystemPrefersDark(): boolean {
  return window.matchMedia?.("(prefers-color-scheme: dark)").matches ?? false;
}

/**
 * Provee y persiste la preferencia de modo claro/oscuro/sistema. No es información sensible
 * (a diferencia de los tokens de sesión — ver Sprint 2), por lo que localStorage es apropiado
 * aquí. Respeta `prefers-color-scheme` por defecto (Nielsen N4: coherencia con el sistema).
 */
export function ThemeModeProvider({ children }: { children: ReactNode }) {
  const [preference, setPreferenceState] = useState<ThemeModePreference>(() => {
    const stored = localStorage.getItem(STORAGE_KEY);
    return stored === "light" || stored === "dark" || stored === "system" ? stored : "system";
  });
  const [systemPrefersDark, setSystemPrefersDark] = useState(getSystemPrefersDark);

  useEffect(() => {
    const mql = window.matchMedia("(prefers-color-scheme: dark)");
    const handler = (e: MediaQueryListEvent) => setSystemPrefersDark(e.matches);
    mql.addEventListener("change", handler);
    return () => mql.removeEventListener("change", handler);
  }, []);

  const setPreference = (p: ThemeModePreference) => {
    setPreferenceState(p);
    localStorage.setItem(STORAGE_KEY, p);
  };

  const effectiveMode: "light" | "dark" =
    preference === "system" ? (systemPrefersDark ? "dark" : "light") : preference;

  const theme = useMemo(() => buildTheme(effectiveMode), [effectiveMode]);

  return (
    <ThemeModeContext.Provider value={{ preference, effectiveMode, setPreference }}>
      <ThemeProvider theme={theme}>
        <CssBaseline />
        {children}
      </ThemeProvider>
    </ThemeModeContext.Provider>
  );
}

export function useThemeMode(): ThemeModeContextValue {
  const ctx = useContext(ThemeModeContext);
  if (!ctx) throw new Error("useThemeMode debe usarse dentro de ThemeModeProvider");
  return ctx;
}
