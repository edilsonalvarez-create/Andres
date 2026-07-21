import { describe, it, expect, vi, beforeEach } from "vitest";

// Mock de axios: el módulo client.ts usa axios.create() (instancia con interceptors) y axios.post().
// vi.hoisted expone `post` a la fábrica de vi.mock (que se eleva al inicio del archivo).
const { post } = vi.hoisted(() => ({ post: vi.fn() }));
vi.mock("axios", () => {
  const instance = {
    interceptors: { request: { use: vi.fn() }, response: { use: vi.fn() } },
    request: vi.fn(),
  };
  return {
    default: { create: vi.fn(() => instance), post },
  };
});

import { getStoredAuth, storeAuth, refreshSession, logoutSession, extractApiErrorMessage } from "./client";
import type { AuthResponse } from "../types";

const auth: AuthResponse = {
  accessToken: "acc", expiresInMinutes: 30, userId: "u1",
  email: "qa@test.com", fullName: "QA", roles: ["QA"],
};

describe("api/client — almacenamiento de sesión", () => {
  beforeEach(() => {
    storeAuth(null);
    post.mockReset();
    document.cookie = "";
  });

  it("guarda y recupera la sesión en memoria", () => {
    storeAuth(auth);
    expect(getStoredAuth()).toEqual(auth);
    storeAuth(null);
    expect(getStoredAuth()).toBeNull();
  });

  it("NO persiste el token en localStorage (regresión de seguridad Sprint 2)", () => {
    storeAuth(auth);
    expect(localStorage.getItem("qaguardian.auth")).toBeNull();
    expect(JSON.stringify(localStorage)).not.toContain("acc");
  });

  it("refreshSession envía el X-CSRF-Token leído de la cookie", async () => {
    document.cookie = "qaguardian_csrf=token-csrf-123";
    post.mockResolvedValueOnce({ data: auth });

    const result = await refreshSession();

    expect(result).toEqual(auth);
    expect(getStoredAuth()).toEqual(auth); // re-hidrata la sesión
    const [, , config] = post.mock.calls[0];
    expect(config.headers["X-CSRF-Token"]).toBe("token-csrf-123");
    expect(config.withCredentials).toBe(true);
  });

  it("refreshSession fallido limpia la sesión y devuelve null", async () => {
    storeAuth(auth);
    post.mockRejectedValueOnce(new Error("401"));

    const result = await refreshSession();

    expect(result).toBeNull();
    expect(getStoredAuth()).toBeNull();
  });

  it("logoutSession limpia la sesión aunque la petición falle", async () => {
    storeAuth(auth);
    post.mockRejectedValueOnce(new Error("network"));

    // La petición puede rechazar, pero el bloque finally siempre limpia el estado local.
    await logoutSession().catch(() => {});

    expect(getStoredAuth()).toBeNull();
  });
});

describe("extractApiErrorMessage", () => {
  it("devuelve el mensaje cuando la respuesta trae { error }", () => {
    const err = { response: { data: { error: "Título requerido." } } };
    expect(extractApiErrorMessage(err)).toBe("Título requerido.");
  });

  it("devuelve undefined si no hay response (red caída)", () => {
    expect(extractApiErrorMessage(new Error("network"))).toBeUndefined();
  });

  it("devuelve undefined si response no trae data", () => {
    expect(extractApiErrorMessage({ response: {} })).toBeUndefined();
  });

  it("devuelve undefined si data no trae error", () => {
    expect(extractApiErrorMessage({ response: { data: {} } })).toBeUndefined();
  });

  it("devuelve undefined para valores no relacionados con axios (null, string, number)", () => {
    expect(extractApiErrorMessage(null)).toBeUndefined();
    expect(extractApiErrorMessage("boom")).toBeUndefined();
    expect(extractApiErrorMessage(42)).toBeUndefined();
  });
});
