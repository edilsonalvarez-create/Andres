# Technical Debt Audit — QA Guardian (Sprint 4)

**Rol**: Software Architect. **Objetivo**: reducir deuda técnica sin cambiar comportamiento.
**Metodología**: lectura completa del código real (tamaños de archivo, estructura, dependencias
entre capas), no heurísticas genéricas. Cada hallazgo cita archivo y línea. Verificado con la
suite de tests completa antes/después de cada refactor (108/108 en verde) y con `tsc`/`vite
build` para el frontend.

---

## Resumen ejecutivo

| Hallazgo | Tipo | Severidad | Estado |
|---|---|---|---|
| `RunReportGenerator` (618 líneas, 3 familias de reporte no relacionadas) | Violación SRP | Alta | ✅ Refactorizado |
| `TestRunnerFactory` (switch + Service Locator) | Violación OCP | Media | ✅ Refactorizado |
| 5 formularios de automatización con boilerplate similar | Duplicación (aparente) | — | ⚠️ Ver hallazgo TD-03 (alcance real: 2 de 5) |
| Capas de Clean Architecture | — | — | ✅ Sin violaciones (verificado) |
| Código muerto / TODOs / bloques comentados | — | — | ✅ Sin hallazgos reales (verificado) |

---

## TD-01 — Violación de SRP: `RunReportGenerator` (618 líneas)

**Ubicación**: `src/QAGuardian.Infrastructure/Reports/RunReportGenerator.cs` (antes del refactor).

**Evidencia**: la clase implementaba `IReportGenerator` y concentraba **3 familias de reporte
sin relación entre sí**, cada una con su propio modelo de datos:

1. Reporte de ejecución (`TestRun` → 8 formatos: JSON, CSV, HTML, Excel, PDF, Word, XML, PowerPoint)
2. Reporte de dashboard (`DashboardDto` → 2 formatos: Excel, PDF)
3. Matriz de ejecución (construcción de `ExecutionMatrixDto` desde la BD + 1 formato: Excel)

Esto ya estaba identificado desde la auditoría inicial del proyecto (Sprint 0) pero había
**crecido** en vez de corregirse: Sprint 1 añadió la matriz de ejecución al mismo archivo en
lugar de a uno nuevo.

**Por qué es un problema real** (no solo "el archivo es largo"): las 3 familias no comparten
lógica de negocio, solo la clase contenedora. Un cambio en el formato Excel del dashboard
obliga a recompilar y volver a probar el generador de reportes de ejecución, pese a no tener
relación. Los tests de una familia (`DashboardReportTests.cs`) instanciaban la clase completa
con dependencias irrelevantes para lo que probaban (`ITestRunRepository`,
`IProjectRepository` — ninguna usada por la ruta de dashboard).

**Refactor aplicado** (Extract Class, patrón de Fowler): la clase se separó en 3 escritores
especializados, con `RunReportGenerator` reducido a un orquestador de ~65 líneas que resuelve
identidad del recurso (buscar `TestRun`/proyecto) y metadatos (content-type, nombre de
archivo), delegando el contenido binario:

- `RunReportWriter` — los 8 formatos de reporte de ejecución (puro, sin dependencias de BD).
- `DashboardReportWriter` — los 2 formatos de dashboard (puro).
- `ExecutionMatrixReportWriter` — construcción de la matriz + Excel (requiere los repositorios).

**Alternativa considerada y descartada**: dividir por *formato* (`IPdfWriter`, `IExcelWriter`,
etc.) en lugar de por *familia de reporte*. Se descartó porque `GeneratePdf(TestRun,...)` y
`GenerateDashboardPdf(DashboardDto,...)` no comparten ninguna lógica real más allá de invocar
la misma librería (QuestPDF) — forzar una interfaz común habría sido una abstracción
especulativa sin beneficio de reuso real (ver ADR-008).

**Verificación de comportamiento preservado**: `DashboardReportTests.cs` (3 casos, ya
existentes) pasa sin modificar una sola línea del test. Contenido, content-type y nombre de
archivo son byte-idénticos (movimiento de método puro, sin cambios de lógica).

---

## TD-02 — Violación de OCP: `TestRunnerFactory`

**Ubicación**: `src/QAGuardian.Infrastructure/Runners/TestRunners.cs:449-458` (antes del refactor).

**Evidencia**:
```csharp
public ITestRunner Resolve(AutomationFramework framework) => framework switch
{
    AutomationFramework.Playwright => _services.GetRequiredService<PlaywrightTestRunner>(),
    AutomationFramework.Postman => _services.GetRequiredService<NewmanTestRunner>(),
    // ... 4 casos más
    _ => throw new NotSupportedException($"Framework no soportado: {framework}")
};
```

**Por qué es un problema real**: agregar soporte para un framework de automatización nuevo
(ej. Cypress, k6) exige (a) crear la clase `ITestRunner`, (b) registrarla en DI, **y (c)
editar esta fábrica** — el paso (c) es exactamente lo que el principio Open/Closed prohíbe
("abierto a extensión, cerrado a modificación"). Adicionalmente, `GetRequiredService<T>()`
sobre `IServiceProvider` inyectado es el anti-patrón *Service Locator*: oculta las
dependencias reales de la clase (no aparecen en el constructor), dificultando saber qué
runners existen sin leer el cuerpo del método.

**Refactor aplicado**: los 6 runners se registran en DI como `ITestRunner` (no por tipo
concreto); `TestRunnerFactory` recibe `IEnumerable<ITestRunner>` en el constructor y arma un
diccionario indexado por `.Framework` (una propiedad que cada runner ya exponía). `Resolve()`
pasa de un `switch` de 6 casos a una búsqueda en diccionario de una línea. Agregar un runner
nuevo ahora es **una sola línea en `DependencyInjection.cs`**, sin tocar la fábrica.

**Verificación de comportamiento preservado**: se agregó `TestRunnerFactoryTests.cs` (10
casos, no existía cobertura previa para esta clase) que fija: resolución correcta por
framework, `NotSupportedException` con el mismo mensaje para un framework sin runner, mapeo
idéntico de `ResolveByTestType`, y — como prueba explícita del fix de OCP — que agregar un
framework nuevo a la colección inyectada no requiere ningún cambio en `TestRunnerFactory`.

---

## TD-03 — Duplicación en formularios de automatización: alcance real tras lectura completa

**Ubicación**: `frontend/src/components/{Playwright,JMeter,Postman,OWASPZAP,SeleniumIDE}Form.tsx`.

**Hallazgo inicial (por métricas de superficie)**: los 5 archivos tienen tamaño similar
(429/398/267/245/230 líneas) y un `grep` de `useEffect`/`updateConfig`/`defaultConfig` mostró
el mismo vocabulario en los 5 — sugiriendo una duplicación total de 5 componentes.

**Hallazgo real (tras leer los 5 completos)**: solo **2 de los 5 son verdaderamente
idénticos** en su lógica de estado. Los otros 3 tienen diferencias de comportamiento reales
que una extracción ingenua habría alterado:

| Formulario | Patrón de estado | ¿Extraíble sin cambiar comportamiento? |
|---|---|---|
| `JMeterForm` | parsear YAML con try/catch → fallback; vacío → `defaultConfig()` + emitir | ✅ Sí |
| `OWASPZAPForm` | idéntico a JMeterForm | ✅ Sí |
| `PostmanForm` | agrega una guarda de regex antes de parsear; reutiliza el estado ya inicializado en la rama vacía | ⚠️ Requeriría un parámetro `canParse` adicional — se dejó fuera para no complejizar el hook por un solo caso |
| `PlaywrightForm` | **nunca** reconstruye el constructor visual desde contenido existente (siempre pasa a modo "código"); tiene un estado adicional `mode` | ❌ No — la rama "con contenido" no comparte lógica |
| `SeleniumIDEForm` | formato `.side` (JSON), no YAML; función de generación con 2 parámetros (`config`, `ids`); estado adicional `ids`/`mode` | ❌ No — formato de datos distinto por completo |

**Decisión**: se extrajo `frontend/src/hooks/useParsedYamlConfig.ts` y se aplicó **únicamente**
a `JMeterForm` y `OWASPZAPForm`, los 2 casos genuinamente idénticos. Forzar los otros 3 en la
misma abstracción habría exigido parámetros opcionales adicionales (`canParse`, soporte para
un segundo argumento en `generate`, un formato de parseo alterno) hasta el punto de que el
hook resultante sería más difícil de entender que la duplicación que pretende eliminar —
exactamente el anti-patrón de abstracción prematura que este sprint busca evitar, no crear.

**Lección de proceso**: las métricas de superficie (tamaño de archivo, conteo de coincidencias
de `grep`) señalan *dónde mirar*, no *qué extraer*. La decisión de extraer solo se tomó
después de leer las 5 implementaciones completas línea por línea.

**Verificación de comportamiento preservado**: `tsc -b --noEmit` sin errores; `npm run build`
exitoso; el hook replica exactamente la secuencia de `setState`/`onChange` original (mismo
orden de efectos, mismo manejo de excepción, mismo caso vacío).

---

## Hallazgos positivos (verificados, no requieren acción)

- **Límites de Clean Architecture respetados**: `grep` confirmó que `Domain` no referencia
  `Application`/`Infrastructure`/`API`, y que `Application` no referencia `Infrastructure`
  directamente — las 4 capas dependen correctamente solo hacia adentro.
- **Sin código muerto real**: no se encontraron bloques de código comentado (los 2 falsos
  positivos de `grep` eran comentarios XML doc legítimos), ni métodos/exports sin referencias.
- **Un solo TODO** en todo el código fuente (`PlaywrightRecorder.cs:67`), y resultó ser texto
  de plantilla generado para que el usuario complete un script grabado — no una tarea
  pendiente real del equipo.
- **Rate limiting, auditoría, RBAC**: sin cambios necesarios; ya cubiertos en Sprint 2.

---

## Backlog (identificado, no abordado en este sprint)

| Hallazgo | Por qué no se aborda ahora |
|---|---|
| `TestRunCommands.cs` (494 líneas) | Es una agrupación vertical de múltiples *command handlers* relacionados de CQRS — patrón deliberado y consistente en todo el proyecto (una carpeta/archivo por *feature*, no por clase), no una violación de SRP por sí sola. Requeriría una revisión caso por caso de cada handler, no un fix mecánico. |
| Mapas de etiquetas duplicados frontend↔backend (`TEST_TYPES`, `PRIORITIES`, etc. en `types.ts` vs los enums reales de C#) | Acoplamiento real pero de baja severidad y común en la mayoría de SPAs que no derivan tipos automáticamente del backend; corregirlo de raíz (generación de tipos desde OpenAPI/Swagger) es un cambio de tooling, no un refactor puntual. |
| Bundle de frontend > 500 kB (advertencia de Vite) | Es una optimización de *build*, no deuda de arquitectura; code-splitting por ruta es candidato para un sprint de performance. |

---

## Verificación final

```
dotnet test QAGuardian.sln           → 108/108 (98 preexistentes + 10 nuevos de TestRunnerFactory)
npx tsc -b --noEmit (frontend)       → 0 errores
npm run build (frontend)             → build de producción exitoso
```

Ningún test existente se modificó para hacerlo pasar — la preservación de comportamiento se
verificó con la suite tal cual estaba antes del sprint.
