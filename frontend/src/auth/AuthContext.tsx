import { createContext, useContext, useEffect, useState, type ReactNode } from "react";
import axios from "axios";
import { getStoredAuth, logoutSession, refreshSession, storeAuth } from "../api/client";
import type { AuthResponse } from "../types";

interface AuthContextValue {
  auth: AuthResponse | null;
  loading: boolean;
  login: (email: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
  hasRole: (...roles: string[]) => boolean;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [auth, setAuth] = useState<AuthResponse | null>(getStoredAuth());
  const [loading, setLoading] = useState(true);

  // El access token vive solo en memoria (nunca en localStorage), así que al recargar la página
  // se pierde. Al montar, se intenta renovar en silencio usando la cookie httpOnly de refresh;
  // si no hay sesión válida, el usuario simplemente ve el login.
  useEffect(() => {
    let cancelled = false;
    refreshSession().then((renewed) => {
      if (!cancelled) {
        setAuth(renewed);
        setLoading(false);
      }
    });
    return () => {
      cancelled = true;
    };
  }, []);

  const login = async (email: string, password: string) => {
    const { data } = await axios.post<AuthResponse>(
      "/api/v1/auth/login",
      { email, password },
      { withCredentials: true }
    );
    storeAuth(data);
    setAuth(data);
  };

  const logout = async () => {
    await logoutSession();
    setAuth(null);
  };

  const hasRole = (...roles: string[]) =>
    auth !== null && roles.some((r) => auth.roles.includes(r));

  return (
    <AuthContext.Provider value={{ auth, loading, login, logout, hasRole }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth debe usarse dentro de AuthProvider");
  return ctx;
}
