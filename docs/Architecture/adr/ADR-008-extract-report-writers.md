# ADR-008: Separar `RunReportGenerator` en escritores por familia de reporte

**Estado**: Aceptado · **Fecha**: 2026-07-16 · **Sprint**: 4

## Contexto

`RunReportGenerator` (618 líneas) implementaba `IReportGenerator` concentrando 3 familias de
reporte sin relación de negocio entre sí:

1. Reporte de ejecución (`TestRun` → JSON/CSV/HTML/Excel/PDF/Word/XML/PowerPoint — 8 formatos)
2. Reporte de dashboard (`DashboardDto` → Excel/PDF)
3. Matriz de ejecución (construcción desde BD + Excel)

Cada familia usa un modelo de datos distinto y no comparte lógica real entre sí más allá de
invocar las mismas librerías (ClosedXML, QuestPDF, OpenXML). El archivo creció en cada sprint
(Sprint 1 añadió la matriz de ejecución al mismo archivo) sin que nadie lo dividiera,
violando el Principio de Responsabilidad Única (SRP): la clase tenía 3 razones distintas para
cambiar.

## Decisión

Extraer cada familia a su propia clase (patrón *Extract Class* de Fowler):

- `RunReportWriter` — 8 formatos de reporte de ejecución (sin dependencias de infraestructura).
- `DashboardReportWriter` — 2 formatos de dashboard (sin dependencias de infraestructura).
- `ExecutionMatrixReportWriter` — construcción de la matriz (requiere `ITestRunRepository`,
  `IProjectRepository`) + su único formato Excel.

`RunReportGenerator` se conserva como implementación de `IReportGenerator` (sin cambiar el
contrato consumido por Application/API) pero reducido a un orquestador delgado: resuelve el
recurso (buscar el `TestRun`/proyecto), calcula nombre de archivo y content-type, y delega el
contenido binario al escritor correspondiente.

## Alternativas consideradas

**Dividir por formato en vez de por familia** (`IPdfWriter`, `IExcelWriter`, etc., cada uno
parametrizado sobre el tipo de dato). Se descartó: `GeneratePdf(TestRun,...)` y
`GenerateDashboardPdf(DashboardDto,...)` no comparten ninguna lógica de negocio real, solo la
librería QuestPDF — una interfaz común habría sido una abstracción especulativa (código
genérico "por si acaso" sin un segundo caso de uso real que lo justifique) y además habría
sido más compleja de leer que las 8+2+1 funciones actuales, ya simples y directas.

## Consecuencias

**Positivas**: cada clase tiene una sola razón para cambiar; los tests de dashboard ya no
instancian dependencias irrelevantes (`ITestRunRepository`/`IProjectRepository` sin usar);
un desarrollador que solo necesita entender el formato Excel del dashboard lee 65 líneas, no
618.

**Negativas / trade-offs**: 3 archivos en vez de 1 (más navegación entre archivos para ver el
conjunto completo); ninguna, ya que el registro en DI no cambió (`IReportGenerator` sigue
siendo la única superficie pública).

**Verificación**: `DashboardReportTests.cs` (3 casos preexistentes) pasa sin modificaciones —
prueba de que el comportamiento externo es idéntico byte a byte.
