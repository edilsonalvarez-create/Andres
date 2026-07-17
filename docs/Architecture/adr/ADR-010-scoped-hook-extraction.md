# ADR-010: Extraer `useParsedYamlConfig` solo para los 2 formularios genuinamente idénticos

**Estado**: Aceptado · **Fecha**: 2026-07-16 · **Sprint**: 4

## Contexto

Los 5 formularios de automatización (`PlaywrightForm`, `JMeterForm`, `PostmanForm`,
`OWASPZAPForm`, `SeleniumIDEForm`) tienen tamaño similar y usan el mismo vocabulario
(`useState`, `useEffect`, `updateConfig`, `defaultConfig`), sugiriendo por métricas de
superficie que los 5 duplican la misma lógica de estado.

Al leer los 5 completos se encontró que **solo `JMeterForm` y `OWASPZAPForm` son
verdaderamente idénticos**: parsean YAML con try/catch y fallback a `defaultConfig()`, y en
contenido vacío calculan+emiten la config inicial. Los otros 3 tienen diferencias de
comportamiento reales:

- `PostmanForm` agrega una guarda de regex antes de parsear y reutiliza el estado ya
  inicializado en la rama de contenido vacío (en vez de recalcular `defaultConfig()`).
- `PlaywrightForm` **nunca** reconstruye el constructor visual desde contenido existente
  (siempre pasa a modo "código" si hay contenido) y mantiene un estado adicional (`mode`).
- `SeleniumIDEForm` persiste en formato `.side` (JSON), no YAML; su función de generación
  toma 2 parámetros (`config`, `ids`) y mantiene estado adicional (`ids`, `mode`).

## Decisión

Extraer `frontend/src/hooks/useParsedYamlConfig.ts` y aplicarlo **únicamente** a `JMeterForm`
y `OWASPZAPForm`. Los otros 3 formularios se dejan sin modificar.

## Alternativas consideradas

**Forzar los 5 en un solo hook parametrizado** (con un `canParse` opcional para Postman, un
segundo argumento opcional de `generate` para SeleniumIDE, y una rama especial para
Playwright que nunca parsea contenido existente). Se descartó: el hook resultante habría
tenido más parámetros opcionales que líneas de lógica útil, y habría sido más difícil de leer
y razonar que las 3 implementaciones específicas que reemplazaba — el anti-patrón exacto de
abstracción prematura ("generalidad especulativa") que este sprint busca eliminar, no
introducir. Tres implementaciones ligeramente distintas y legibles son preferibles a una
abstracción única con seis interruptores de comportamiento.

## Consecuencias

**Positivas**: `JMeterForm` y `OWASPZAPForm` perdieron ~15 líneas de boilerplate idéntico cada
uno; un futuro formulario de automatización que siga el patrón simple (parsear YAML con
fallback) puede reusar el hook directamente.

**Negativas / trade-offs**: la duplicación entre `PostmanForm`/`PlaywrightForm`/
`SeleniumIDEForm` y el resto **no se eliminó** — es deuda técnica real que permanece, mejor
documentada que ignorada. Ver `Technical-Debt-Audit.md` (TD-03) y considerar en un sprint
futuro si vale la pena introducir parámetros opcionales al hook una vez que exista un tercer
caso de uso genuinamente compatible (evitar generalizar a partir de un solo caso).

**Verificación**: `tsc -b --noEmit` sin errores; `npm run build` exitoso; el hook replica
exactamente el orden de efectos y manejo de excepciones original de ambos formularios.
