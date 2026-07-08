import axios, { AxiosError } from "axios";
import type { AuthResponse } from "../types";

const STORAGE_KEY = "qaguardian.auth";

export function getStoredAuth(): AuthResponse | null {
  const raw = localStorage.getItem(STORAGE_KEY);
  return raw ? (JSON.parse(raw) as AuthResponse) : null;
}

export function storeAuth(auth: AuthResponse | null) {
  if (auth) localStorage.setItem(STORAGE_KEY, JSON.stringify(auth));
  else localStorage.removeItem(STORAGE_KEY);
}

export const api = axios.create({ baseURL: "/api/v1" });

api.interceptors.request.use((config) => {
  const auth = getStoredAuth();
  if (auth) config.headers.Authorization = `Bearer ${auth.accessToken}`;
  return config;
});

let refreshing: Promise<AuthResponse | null> | null = null;

async function refreshToken(): Promise<AuthResponse | null> {
  const auth = getStoredAuth();
  if (!auth) return null;
  try {
    const { data } = await axios.post<AuthResponse>("/api/v1/auth/refresh", {
      refreshToken: auth.refreshToken,
    });
    storeAuth(data);
    return data;
  } catch {
    storeAuth(null);
    return null;
  }
}

api.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const original = error.config;
    if (error.response?.status === 401 && original && !original.headers["X-Retried"]) {
      refreshing ??= refreshToken().finally(() => (refreshing = null));
      const renewed = await refreshing;
      if (renewed) {
        original.headers["X-Retried"] = "1";
        original.headers.Authorization = `Bearer ${renewed.accessToken}`;
        return api.request(original);
      }
      window.location.href = "/login";
    }
    return Promise.reject(error);
  }
);
