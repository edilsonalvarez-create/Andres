import { describe, it, expect, vi } from "vitest";
import { renderHook, act } from "@testing-library/react";
import { useParsedYamlConfig } from "./useParsedYamlConfig";

interface Cfg { threads: number }

const defaultCfg = (): Cfg => ({ threads: 10 });
const parse = (c: string): Cfg => ({ threads: Number(c.match(/threads:\s*(\d+)/)?.[1] ?? 0) });
const generate = (c: Cfg) => `threads: ${c.threads}`;

describe("useParsedYamlConfig", () => {
  it("con contenido vacío usa el default y emite su YAML al montar", () => {
    const onChange = vi.fn();
    const { result } = renderHook(() =>
      useParsedYamlConfig("", onChange, defaultCfg, parse, generate));

    expect(result.current[0]).toEqual({ threads: 10 });
    // Nuevo script: emite el YAML inicial para habilitar Guardar/Ejecutar.
    expect(onChange).toHaveBeenCalledWith("threads: 10");
  });

  it("con contenido existente lo parsea y NO reemite al montar", () => {
    const onChange = vi.fn();
    const { result } = renderHook(() =>
      useParsedYamlConfig("threads: 50", onChange, defaultCfg, parse, generate));

    expect(result.current[0]).toEqual({ threads: 50 });
    expect(onChange).not.toHaveBeenCalled();
  });

  it("si el parseo lanza, cae al default sin romper", () => {
    const onChange = vi.fn();
    const throwingParse = () => { throw new Error("yaml inválido"); };
    const { result } = renderHook(() =>
      useParsedYamlConfig("basura", onChange, defaultCfg, throwingParse, generate));

    expect(result.current[0]).toEqual({ threads: 10 });
  });

  it("updateConfig actualiza el estado y re-emite el YAML", () => {
    const onChange = vi.fn();
    const { result } = renderHook(() =>
      useParsedYamlConfig("threads: 5", onChange, defaultCfg, parse, generate));

    act(() => result.current[1]({ threads: 99 }));

    expect(result.current[0]).toEqual({ threads: 99 });
    expect(onChange).toHaveBeenLastCalledWith("threads: 99");
  });
});
