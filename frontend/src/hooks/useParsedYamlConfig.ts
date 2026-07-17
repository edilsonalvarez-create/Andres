import { useEffect, useState } from "react";

/**
 * Estado de un formulario cuyo script persistido es YAML: al montar, intenta parsear
 * `content`; si falla o está vacío, usa `defaultConfig()` y emite su YAML de inmediato para
 * habilitar Guardar/Ejecutar sin que el usuario toque nada. `updateConfig` actualiza el
 * estado y re-emite el YAML en cada cambio.
 *
 * Extraído de JMeterForm y OWASPZAPForm (Sprint 4, ADR-010) — únicos 2 de los 5 formularios de
 * automatización con este patrón exactamente idéntico. Postman (guarda de formato antes de
 * parsear), Playwright (nunca reconstruye el constructor desde contenido existente, solo
 * desde vacío) y SeleniumIDE (formato .side/JSON, estado adicional de ids) tienen variaciones
 * reales que esta abstracción no fuerza, para no alterar su comportamiento — ver
 * Technical-Debt-Audit.md.
 */
export function useParsedYamlConfig<T>(
  content: string,
  onChange: (content: string) => void,
  defaultConfig: () => T,
  parseYaml: (content: string) => unknown,
  generateYaml: (config: T) => string
): [T, (next: T) => void] {
  const [config, setConfig] = useState<T>(defaultConfig());

  useEffect(() => {
    if (content && content.trim()) {
      try {
        setConfig(parseYaml(content) as T);
      } catch {
        setConfig(defaultConfig());
      }
    } else {
      const initial = defaultConfig();
      setConfig(initial);
      onChange(generateYaml(initial));
    }
    // Solo al montar / cambiar de caso — replica el comportamiento original de cada formulario.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const updateConfig = (next: T) => {
    setConfig(next);
    onChange(generateYaml(next));
  };

  return [config, updateConfig];
}
