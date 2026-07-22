# Diagramas de arquitectura — Sprint 4

Diagramas Mermaid (se renderizan nativamente en GitHub/GitLab y en el visor de Markdown del
editor) que ilustran los refactors de este sprint. Ver `Technical-Debt-Audit.md` para el
detalle textual y los ADR-008/009/010 para la justificación de cada decisión.

---

## 1. Capas de Clean Architecture (verificado sin violaciones)

```mermaid
graph TD
    API["QAGuardian.API<br/>Controllers · SignalR · JWT/RBAC · Swagger"]
    APP["QAGuardian.Application<br/>CQRS (MediatR) · FluentValidation · Puertos"]
    DOM["QAGuardian.Domain<br/>Agregados · Entidades · Reglas de negocio"]
    INFRA["QAGuardian.Infrastructure<br/>EF Core · Runners · IA · Reportes · Clientes externos"]

    API --> APP
    API --> INFRA
    APP --> DOM
    INFRA --> APP
    INFRA --> DOM

    style DOM fill:#e8eaf6,stroke:#3f51b5,stroke-width:2px
    style APP fill:#f4f6fb,stroke:#16213e
    style INFRA fill:#f4f6fb,stroke:#16213e
    style API fill:#f4f6fb,stroke:#16213e
```

**Verificado por `grep`** (Sprint 4): `Domain` no importa `Application`/`Infrastructure`/`API`;
`Application` no importa `Infrastructure` directamente. Las flechas apuntan siempre hacia
adentro, como exige Clean Architecture — sin excepciones encontradas.

---

## 2. `RunReportGenerator` — antes (SRP violado)

```mermaid
classDiagram
    class RunReportGenerator {
        <<618 líneas>>
        -ITestRunRepository _runs
        -IProjectRepository _projects
        +GenerateRunReportAsync() 8 formatos
        +GenerateDashboardReport() 2 formatos
        +GetExecutionMatrixAsync()
        +GenerateExecutionMatrixReportAsync()
        -GenerateJson() -GenerateCsv() -GenerateHtml()
        -GenerateExcel() -GeneratePdf() -GenerateWord()
        -GenerateXml() -GeneratePowerPoint()
        -GenerateDashboardExcel() -GenerateDashboardPdf()
        -BuildExecutionMatrixAsync() -GenerateExecutionMatrixExcel()
    }
    class IReportGenerator {
        <<interface>>
    }
    IReportGenerator <|.. RunReportGenerator
    note for RunReportGenerator "3 familias de reporte sin\nrelación de negocio entre sí\nen una sola clase (ADR-008)"
```

## 2b. `RunReportGenerator` — después (Extract Class)

```mermaid
classDiagram
    class IReportGenerator {
        <<interface>>
    }
    class RunReportGenerator {
        <<~65 líneas — orquestador>>
        -RunReportWriter _runReportWriter
        -DashboardReportWriter _dashboardWriter
        -ExecutionMatrixReportWriter _matrixWriter
        +GenerateRunReportAsync()
        +GenerateDashboardReport()
        +GetExecutionMatrixAsync()
        +GenerateExecutionMatrixReportAsync()
    }
    class RunReportWriter {
        <<8 formatos de TestRun>>
        +Write(run, projectName, format) byte[]
    }
    class DashboardReportWriter {
        <<2 formatos de DashboardDto>>
        +Write(stats, title, format) byte[]
    }
    class ExecutionMatrixReportWriter {
        <<requiere repositorios>>
        +GetMatrixAsync()
        +GenerateExcelReportAsync()
    }

    IReportGenerator <|.. RunReportGenerator
    RunReportGenerator --> RunReportWriter : delega
    RunReportGenerator --> DashboardReportWriter : delega
    RunReportGenerator --> ExecutionMatrixReportWriter : delega
```

---

## 3. `TestRunnerFactory` — antes (OCP violado, Service Locator)

```mermaid
sequenceDiagram
    participant C as ExecuteTestRunCommandHandler
    participant F as TestRunnerFactory
    participant SP as IServiceProvider
    participant R as PlaywrightTestRunner

    C->>F: Resolve(AutomationFramework.Playwright)
    Note over F: switch (framework) { ... }
    F->>SP: GetRequiredService&lt;PlaywrightTestRunner&gt;()
    Note over SP: Service Locator: dependencia<br/>oculta, no visible en el constructor
    SP-->>F: instancia
    F-->>C: ITestRunner
    Note over F: Agregar un framework nuevo =<br/>editar este switch (viola OCP)
```

## 3b. `TestRunnerFactory` — después (búsqueda por diccionario inyectado)

```mermaid
sequenceDiagram
    participant DI as Contenedor DI
    participant F as TestRunnerFactory
    participant C as ExecuteTestRunCommandHandler

    DI->>F: new(IEnumerable&lt;ITestRunner&gt; runners)
    Note over F: _runnersByFramework =<br/>runners.ToDictionary(r => r.Framework)
    C->>F: Resolve(AutomationFramework.Playwright)
    Note over F: diccionario[framework] — sin switch
    F-->>C: ITestRunner
    Note over F: Agregar un framework nuevo =<br/>1 línea en DependencyInjection.cs,<br/>cero cambios aquí (cumple OCP)
```

---

## 4. Alcance real de la duplicación en formularios de automatización

```mermaid
graph LR
    subgraph Idénticos["✅ Extraídos a useParsedYamlConfig"]
        JM[JMeterForm]
        ZAP[OWASPZAPForm]
    end
    subgraph Distintos["❌ Comportamiento propio — no forzados en el hook"]
        PM["PostmanForm<br/><i>guarda de regex extra</i>"]
        PW["PlaywrightForm<br/><i>nunca parsea contenido existente</i>"]
        SE["SeleniumIDEForm<br/><i>formato .side JSON, no YAML</i>"]
    end
    H[["useParsedYamlConfig&lt;T&gt;()"]]
    JM --> H
    ZAP --> H

    style Idénticos fill:#e8f5e9,stroke:#2e7d32
    style Distintos fill:#fff3e0,stroke:#ed6c02
```

**Lección del sprint**: el tamaño de archivo similar (429/398/267/245/230 líneas) y el
vocabulario compartido (`useEffect`, `updateConfig`, `defaultConfig`) sugerían 5 duplicados
idénticos. Leer las 5 implementaciones completas reveló que solo 2 lo son genuinamente — ver
ADR-010.
