# Testing Strategy — QA Guardian (Sprint 5)

**Rol**: QA Automation Architect. **Objetivo del sprint**: 80% backend, 70% frontend, 60% E2E.
**Principio**: cobertura funcional real, sin pruebas triviales. Cada número de este documento se
**midió** (no se estimó); cuando un objetivo no se alcanzó en el sprint, se dice explícitamente
y se documenta el camino concreto para llegar.

---

## 1. La pirámide de pruebas de QA Guardian

```
        ╱╲          E2E (Playwright)  ── camino crítico, smoke, visual
       ╱  ╲                             pocos, lentos, alto valor de confianza
      ╱────╲        Integración/API (WebApplicationFactory)  ── HTTP real, RBAC, contrato
     ╱      ╲                           medios; prueban el cableado entre capas
    ╱────────╲      Unitarias (xUnit + NSubstitute / Vitest + Testing Library)
   ╱__________╲                         muchas, rápidas; lógica de negocio pura
```

Herramientas por nivel:

| Nivel | Backend | Frontend |
|---|---|---|
| Unitario | xUnit + NSubstitute + FluentAssertions + Coverlet | Vitest + Testing Library + jsdom |
| Integración / API / Contrato | `WebApplicationFactory<Program>` (API real sobre SQLite en memoria) | — |
| E2E / Smoke / Visual | — | Playwright (Chromium) |
| Mutación | Stryker.NET | — |

---

## 2. Resultados medidos (antes → después)

### Backend — cobertura de línea (Coverlet)

| Módulo | Antes (baseline Sprint 5) | Después |
|---|---|---|
| QAGuardian.Application | 21.39% | **38.25%** |
| QAGuardian.Domain | 65.59% | **68.20%** |
| QAGuardian.Infrastructure | 12.79% | **37.69%** |
| **Total** | **19.66%** | **42.53%** |

- Pruebas unitarias: **108 → 166** (+58 nuevas, todas con lógica real de negocio).
- Pruebas de integración/API: **5 → 11** (+6).
- Se excluyeron de cobertura las **migraciones EF generadas** (~3.700 líneas de scaffolding de
  herramienta, no lógica): sin esa exclusión el total "real" quedaba artificialmente en 23%.
- Gate de cobertura en CI subido de **15% → 35%** (`Threshold` en el csproj de pruebas).

### Backend — mutación (Stryker.NET)

Acotado a las máquinas de estado del dominio (`Defect`, `QualityGate`, `QualityGateCondition`),
las piezas de mayor densidad de reglas:

- **Mutation score: 62.67%** — 47 mutantes eliminados, 10 sobrevivientes, 0 timeouts.
- Interpretación: las pruebas **detectan el 82% de las mutaciones** en esos archivos (47/57);
  los 10 sobrevivientes marcan condiciones de borde donde reforzar aserciones (ver el reporte
  HTML en `StrykerOutput/`). La cobertura de línea sola no revelaría estos huecos — por eso la
  mutación complementa a Coverlet.

### Frontend — Vitest

Infraestructura creada desde cero (antes: **0 pruebas, sin runner**).

- **25 pruebas** en 5 archivos, todas en verde.
- Cobertura de los componentes/lógica probados: `DataTable` 98.4%, `EmptyState` 100%,
  `ConfirmDialog` cubierto, `useParsedYamlConfig` 100%, `api/client` 75% (incluye una
  **regresión de seguridad**: verifica que los tokens viven solo en memoria, nunca en
  localStorage — Sprint 2).
- Cobertura **global del frontend: baja** (~4%) porque las decenas de componentes de *página*
  (Dashboard, TestRuns, los 5 formularios, Layout, etc.) aún no tienen pruebas. Ver §4.

### E2E — Playwright

Infraestructura creada desde cero (antes: **nada**). 9 specs en 3 archivos:

- **Smoke (4/4 en verde contra el stack vivo)**: carga del login, error de credenciales
  accesible, login real de punta a punta hasta el dashboard, y navegación por la paleta de
  comandos (Ctrl+K). Ejecutados con navegador real + API real + login real por la UI.
- **Camino crítico (2 specs)**: crear proyecto → crear caso de prueba; preset de rango del
  dashboard. Compilan y se descubren; dependen de una cuenta de prueba con credenciales
  conocidas (ver §5, "cómo ejecutar").
- **Regresión visual (3 specs, 2 baselines verificadas)**: `login.png` y `login-dark.png`
  generadas y **comparadas en verde** (prueba de que la comparación píxel a píxel funciona);
  la del dashboard enmascara contenido dinámico y requiere sesión.

---

## 3. Decisiones de arquitectura de pruebas

### 3.1 Playwright, no Cypress (una sola herramienta E2E)

El prompt listaba ambos. **Montar dos frameworks E2E que hacen el mismo trabajo es deuda de
mantenimiento, no cobertura adicional** — duplicaría helpers, fixtures, CI y curva de
aprendizaje sin ganar confianza. Se eligió **Playwright** por tres razones concretas de este
proyecto:

1. **Coherencia con la propia plataforma**: QA Guardian ya usa Playwright en su
   `VisualRegressionRunner` y en su recorder — el equipo ya conoce la herramienta.
2. **Regresión visual y trazas integradas** de fábrica (Cypress las delega a plugins de pago
   o de terceros).
3. **Multi-navegador y paralelismo** nativos.

Si en el futuro se requiere Cypress por una razón específica (p. ej. un equipo que ya lo
domina), la recomendación es **migrar**, no coexistir.

### 3.2 API Tests = Integración con `WebApplicationFactory`

No se montó una herramienta de "API testing" separada (Postman/RestAssured): las pruebas de
API son las de integración, que levantan la API real y ejercen HTTP de verdad (login, CRUD,
RBAC 403, validación 400). Es el mismo objetivo con menos piezas móviles.

### 3.3 Contract Tests = validación del documento OpenAPI

El contrato se verifica en `ApiContractAndRbacTests.Swagger_expone_un_documento_OpenAPI...`:
afirma que `/swagger/v1/swagger.json` es OpenAPI 3.x, expone los recursos clave y declara el
esquema de seguridad Bearer. Es un contract test consumer-agnostic sin introducir Pact.

### 3.4 Exclusión de código generado

Las migraciones EF y el snapshot del modelo se excluyen de cobertura (`ExcludeByFile` en el
csproj de pruebas): medir cobertura sobre scaffolding autogenerado distorsiona el número real
y nunca se prueba a mano.

---

## 4. Brecha honesta a 80 / 70 / 60 y camino para cerrarla

Los tres objetivos **no se alcanzaron en su totalidad en este sprint** — el punto de partida
era 19.66% / 0% / 0% y llevar los tres a la meta es trabajo de varios días. Lo entregado es la
infraestructura completa de los diez tipos de prueba pedidos + una subida real y medible.
El camino concreto restante:

| Meta | Estado | Qué falta (priorizado por ROI) |
|---|---|---|
| Backend 80% | 42.53% | Cubrir `TestRunCommands` (494 líneas, orquestación con Hangfire/runners — requiere tests de integración con job scheduler), los runners externos (Playwright/JMeter/ZAP/Newman — proceso externo, mejor con integración), los clientes HTTP (GitHub/Sonar — con `HttpMessageHandler` mock), `DashboardQueries`, `IntegrationQueries`, `NotificationDispatcher`. Estimado: ~2-3 días. |
| Frontend 70% | ~4% global (componentes compartidos 98-100%) | Probar los componentes de *página* (Dashboard, TestRuns, Projects, Defects, TestCases, los 5 formularios, Layout). Muchas son vistas delgadas de cableado a la API — se prueban con `msw` (mock service worker) para las llamadas. Estimado: ~2-3 días. |
| E2E 60% | Smoke 4/4 + visual verde | Estabilizar el camino crítico (crear proyecto/caso), agregar flujos de defectos, quality gates y ejecución de pruebas; requiere una BD sembrada con credenciales conocidas en CI. Estimado: ~1-2 días. |

Ninguna de estas brechas es un bloqueo de diseño: la infraestructura, los patrones y los
ejemplos de referencia ya están en el repo; es volumen de pruebas siguiendo los mismos moldes.

---

## 5. Cómo ejecutar

```bash
# Backend — unitarias + cobertura
dotnet test tests/QAGuardian.UnitTests -p:CollectCoverage=true -p:CoverletOutputFormat=json

# Backend — integración / API / contrato (requiere que no haya otra API bloqueando el build)
dotnet test tests/QAGuardian.IntegrationTests

# Backend — mutación (acotada a dominio; ~1-2 min)
cd tests/QAGuardian.UnitTests && dotnet stryker      # reporte HTML en StrykerOutput/

# Frontend — unitarias/componente + cobertura
cd frontend && npm run test          # o: npm run test:coverage

# E2E — requiere API en :5080 y navegador instalado
#   1) BD limpia con credenciales conocidas (evita el bloqueo por intentos fallidos):
#      ConnectionStrings__DefaultConnection="Data Source=/tmp/e2e.db" dotnet run --project src/QAGuardian.API
#   2) npx playwright install chromium   (una vez)
#   3) credenciales por env: E2E_EMAIL / E2E_PASSWORD (default: admin de dev)
cd frontend && npx playwright test                    # smoke + critical-path + visual
cd frontend && npx playwright test visual.spec.ts --update-snapshots   # regenerar baselines
```

**Nota de entorno (Sprint 5)**: los tests de integración y E2E requieren que no exista un
proceso `QAGuardian.API` previo bloqueando la salida de compilación; durante este sprint se
detuvo una instancia obsoleta que lo impedía. Las pruebas E2E de login exigen una BD sembrada
con la contraseña configurada (el seed no reescribe un admin ya existente con otra clave).

---

## 6. Inventario de los diez tipos solicitados

| Tipo | Entregado | Ubicación |
|---|---|---|
| Unit Tests | ✅ +58 backend, +25 frontend | `tests/QAGuardian.UnitTests`, `frontend/src/**/*.test.tsx` |
| Integration Tests | ✅ +6 | `tests/QAGuardian.IntegrationTests` |
| Playwright | ✅ smoke 4/4 vivo | `frontend/e2e/*.spec.ts` |
| Cypress | ⚠️ Recomendado NO usar (una sola herramienta E2E) | §3.1 |
| API Tests | ✅ = integración HTTP | `ApiIntegrationTests`, `ApiContractAndRbacTests` |
| Contract Tests | ✅ OpenAPI shape + Bearer | `ApiContractAndRbacTests.Swagger_expone...` |
| Regression | ✅ toda la suite es regresión; + writers de reporte fijan salida | `RunReportWriterTests`, suite completa |
| Smoke | ✅ 4/4 vivo | `frontend/e2e/smoke.spec.ts` |
| Visual Testing | ✅ 2 baselines verificadas | `frontend/e2e/visual.spec.ts` |
| Mutation Testing | ✅ score 62.67% en dominio | `tests/QAGuardian.UnitTests/stryker-config.json` |
