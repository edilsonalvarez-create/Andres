import axios, { AxiosError } from "axios";
import type { AuthResponse } from "../types";

const CSRF_COOKIE_NAME = "qaguardian_csrf";
const CSRF_HEADER_NAME = "X-CSRF-Token";

// OWASP A05:2025 / localStorage-XSS: el estado de sesión vive solo en memoria (esta variable de
// módulo), nunca en localStorage/sessionStorage. Un XSS activo aún podría leerlo mientras la
// pestaña está abierta, pero no queda nada persistente que exfiltrar tras cerrarla. El refresh
// token nunca llega a JavaScript: solo existe en una cookie httpOnly que el navegador administra.
let currentAuth: AuthResponse | null = null;

export function getStoredAuth(): AuthResponse | null {
  return currentAuth;
}

export function storeAuth(auth: AuthResponse | null) {
  currentAuth = auth;
}

function readCookie(name: string): string | null {
  const match = document.cookie.match(new RegExp(`(?:^|; )${name}=([^;]*)`));
  return match ? decodeURIComponent(match[1]) : null;
}

export const api = axios.create({ baseURL: "/api/v1", withCredentials: true });

api.interceptors.request.use((config) => {
  if (currentAuth) config.headers.Authorization = `Bearer ${currentAuth.accessToken}`;
  return config;
});

let refreshing: Promise<AuthResponse | null> | null = null;

/** Renueva la sesión usando la cookie httpOnly de refresh (double-submit CSRF). */
export async function refreshSession(): Promise<AuthResponse | null> {
  try {
    const csrfToken = readCookie(CSRF_COOKIE_NAME);
    const { data } = await axios.post<AuthResponse>(
      "/api/v1/auth/refresh",
      {},
      { withCredentials: true, headers: csrfToken ? { [CSRF_HEADER_NAME]: csrfToken } : {} }
    );
    storeAuth(data);
    return data;
  } catch {
    storeAuth(null);
    return null;
  }
}

export async function logoutSession(): Promise<void> {
  const csrfToken = readCookie(CSRF_COOKIE_NAME);
  try {
    await axios.post(
      "/api/v1/auth/logout",
      {},
      { withCredentials: true, headers: csrfToken ? { [CSRF_HEADER_NAME]: csrfToken } : {} }
    );
  } finally {
    storeAuth(null);
  }
}

api.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const original = error.config;
    if (error.response?.status === 401 && original && !original.headers["X-Retried"]) {
      refreshing ??= refreshSession().finally(() => (refreshing = null));
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

/**
 * Extrae el mensaje `{ error: string }` que la API expone en el body de una
 * respuesta fallida. Devuelve `undefined` si la respuesta no tiene ese shape
 * (red caída, error inesperado, etc.); el llamador decide su propio fallback
 * con `extractApiErrorMessage(err) ?? "mensaje por defecto"`.
 */
export function extractApiErrorMessage(err: unknown): string | undefined {
  return (err as { response?: { data?: { error?: string } } })?.response?.data?.error;
}
