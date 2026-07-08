import { createContext, useContext, useState, type ReactNode } from "react";
import axios from "axios";
import { getStoredAuth, storeAuth } from "../api/client";
import type { AuthResponse } from "../types";

interface AuthContextValue {
  auth: AuthResponse | null;
  login: (email: string, password: string) => Promise<void>;
  logout: () => void;
  hasRole: (...roles: string[]) => boolean;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [auth, setAuth] = useState<AuthResponse | null>(getStoredAuth());

  const login = async (email: string, password: string) => {
    const { data } = await axios.post<AuthResponse>("/api/v1/auth/login", { email, password });
    storeAuth(data);
    setAuth(data);
  };

  const logout = () => {
    storeAuth(null);
    setAuth(null);
  };

  const hasRole = (...roles: string[]) =>
    auth !== null && roles.some((r) => auth.roles.includes(r));

  return (
    <AuthContext.Provider value={{ auth, login, logout, hasRole }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth debe usarse dentro de AuthProvider");
  return ctx;
}
