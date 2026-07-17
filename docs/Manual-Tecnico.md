# Manual Técnico — QA Guardian

## 1. API REST

Base: `/api/v1` · Autenticación: `Authorization: Bearer <JWT>` · Documentación viva:
Swagger UI en `/swagger` (ambiente Development). Versionado por segmento de URL
(`Asp.Versioning`, versión por defecto 1.0).

### Autenticación

| Método | Ruta | Descripción | Roles |
|---|---|---|---|
| POST | `/auth/login` | Login → access + refresh token | Anónimo (rate limit 10/min) |
| POST | `/auth/refresh` | Rotación de refresh token | Anónimo |
| POST | `/auth/register` | Alta de usuario con roles | Administrador |

### Recursos principales

| Recurso | Operaciones | Políticas |
|---|---|---|
| `/projects` | CRUD + `POST {id}/modules` | Lectura: autenticado · Escritura: Admin/LiderTecnico/QA |
| `/testcases` | CRUD + `POST {id}/automate` (vincula script Playwright/Postman/JMeter/ZAP) | Ídem |
| `/testruns` | `GET`, `POST` (encola ejecución), `POST {id}/cancel`, `GET {id}/report?format=Pdf|Excel|Word|PowerPoint|Html|Json|Csv|Xml`, `GET evidence?path=` | Ejecución: Admin/QA/DevOps/LiderTecnico |
| `/defects` | `GET`, `POST`, `POST {id}/status` (workflow Nuevo→…→Cerrado) | Admin/QA/Desarrollador/LiderTecnico |
| `/qualitygates` | `GET`, `POST`, `POST assign` | Escritura: gestión de proyectos |
| `/dashboard` | `GET ?projectId=&from=&to=` KPIs, tendencia, errores por módulo, **heatmap módulo×día**, disponibilidad · `GET report?projectId=&from=&to=&format=Pdf\|Excel` | Autenticado |
| `/catalog` | Requerimientos (`GET modules/{id}/requirements`, `POST requirements`) · Historias (`GET requirements/{id}/stories`, `POST stories`) · Versiones (`GET projects/{id}/versions`, `POST versions`, `POST versions/{id}/release`) · `GET projects/{id}/modules` | Lectura: autenticado · Escritura: gestión de proyectos |
| `/integrations` | `POST` upsert config · `GET sonarqube/{projectId}` · `GET github/{projectId}/pulls` · `POST github/{projectId}/pulls/{n}/analyze` · `POST database-validation` · `POST postman/import` · `GET github/{projectId}/pipeline?download=` | Ver tabla de roles |
| `/admin/users`, `/admin/notification-channels` | Gestión de usuarios y canales | Administrador / gestión |

### Tiempo real (SignalR)

Hub: `/hubs/testruns`. Autenticación: **Authorization Bearer** vía `accessTokenFactory`
(negotiate / LongPolling). En **Production** el cliente usa LongPolling (el handshake
WebSocket del navegador no admite cabecera Authorization) y el API **rechaza**
`?access_token=` en query. En Development el query token aún se acepta con warning en log.

Métodos cliente→servidor: `SubscribeToRun(runId)`, `UnsubscribeFromRun(runId)`.
Eventos servidor→cliente: `runStatusChanged {testRunId, status}`,
`runCompleted {testRunId, passed, failed, gateStatus}`.

**Multi-réplica:** configurar `ConnectionStrings:Redis`. Con Redis definido, SignalR usa
backplane `AddStackExchangeRedis` (canal `qaguardian-signalr`). Sin Redis y ≥2 réplicas,
el progreso en tiempo real será inconsistente (cada nodo solo notifica a sus clientes).

## 2. Configuración (appsettings / variables de entorno)

| Clave | Descripción |
|---|---|
| `ConnectionStrings:DefaultConnection` | SQL Server (o archivo SQLite si `Database:UseSqlite=true`) |
| `ConnectionStrings:Redis` | Caché distribuida + **backplane SignalR** (obligatorio con ≥2 réplicas API). Vacío en Dev = memoria local / SignalR in-proc |
| `Jwt:SigningKey/Issuer/Audience/AccessTokenMinutes/RefreshTokenDays` | Emisión y validación JWT local |
| `Oidc:Authority/Audience/RequireHttpsMetadata` | **OAuth2/OpenID Connect**: al configurar `Authority` (p. ej. `https://login.microsoftonline.com/{tenant}/v2.0` para Entra ID, o la URL de Keycloak/Google), los tokens del proveedor se validan por *discovery* en un segundo esquema. El esquema se elige por el emisor del token; el usuario debe estar **aprovisionado** en QA Guardian (mismo correo) y sus roles RBAC se toman de la base local |
| `Security:EncryptionKey` | AES-256 para tokens de integraciones en reposo |
| `Anthropic:ApiKey` | Agente IA (vacío = modo heurístico) |
| `Smtp:*` | Correo saliente |
| `Storage:EvidencePath` | Raíz de evidencias (default `./storage/evidence`) |
| `Cors:AllowedOrigins` | Lista blanca del frontend |
| `Hangfire:UseInMemory` | `true` para jobs en memoria (dev/test) |
| `Seed:AdminEmail/AdminPassword` | Usuario inicial |

## 3. Integraciones por proyecto

Se configuran con `POST /api/v1/integrations` (el token viaja una vez y se cifra):

| `type` | `baseUrl` | `extraJson` | Uso |
|---|---|---|---|
| 1 SonarQube | URL del servidor | `{"projectKey":"mi-proyecto"}` | Coverage, duplicación, bugs, hotspots, vulnerabilidades, code smells y estado del gate Sonar |
| 2 GitHub | `https://api.github.com` | `{"repository":"owner/repo"}` | PRs, checks, comentarios, releases, dispatch de workflows, archivos del PR |
| 3 OWASP ZAP | — (se usa Docker) | `{"targetUrl":"https://app-qa"}` | Escaneo baseline con parseo de SQLi/XSS/CSRF/Headers/Cookies |
| 4 JMeter | — | — | Los planes `.jmx` se vinculan como scripts de casos tipo Rendimiento |
| 5 Postman | — | `{"postmanEnvironment":"qa.postman_environment.json"}` | Collections vía Newman |

### Vincular automatización a un caso de prueba

`POST /testcases/{id}/automate` con `{framework, scriptPath}`:
- Playwright: ruta a la spec `.spec.ts`
- Postman: ruta a la collection `.json`
- JMeter: ruta al plan `.jmx`
- ZAP: URL objetivo del escaneo
- VisualRegression: URL objetivo de la captura (regresión visual)

Al iniciar una ejecución, `TestRunnerFactory` selecciona el motor según el tipo de la
ejecución y recolecta los scripts activos del proyecto para ese tipo.

### Regresión visual (detectar cambios visuales)

Los casos de tipo **Visual** se ejecutan con `VisualRegressionRunner`:

1. Captura la pantalla completa de la URL con `npx playwright screenshot`.
2. **Primera ejecución del escenario**: la captura se promueve a *baseline* (tabla
   `VisualBaselines`, clave = id del caso o nombre del escenario) y el resultado queda
   en verde con la nota "baseline creado".
3. **Ejecuciones siguientes**: compara la captura actual contra el baseline píxel a píxel
   (`ImageSharpComparer`), genera la imagen de diferencias (píxeles distintos en rojo) y
   adjunta las **tres imágenes** (baseline, actual, diff) como evidencia.
4. **Aprueba** si el porcentaje de píxeles distintos ≤ `ThresholdPercent` (por defecto
   0,10 %); si las dimensiones cambian, se considera cambio total. La tolerancia por píxel
   (`PixelTolerance`, |ΔR|+|ΔG|+|ΔB|) y el umbral son configurables por baseline.

Para re-establecer una referencia tras un cambio de diseño aprobado, se usa
`VisualBaseline.UpdateBaseline(...)` (re-baseline).

### Importar collections de Postman

`POST /integrations/postman/import` con `{projectId, collectionJson, environmentJson?}`:
valida el JSON, cuenta requests (recursivo) y variables, almacena la collection (y el
environment si se envía) y **registra un caso de prueba de tipo API** con framework Postman
listo para ejecutarse con Newman. El environment se guarda en la integración Postman del
proyecto y se inyecta automáticamente (`-e`) en las ejecuciones de tipo API.

### Generar pipeline de GitHub Actions

`GET /integrations/github/{projectId}/pipeline` genera el YAML del pipeline del proyecto con
el flujo Build → Unit Test → Playwright → API Test → Security → Performance → Deploy Staging
→ Smoke Test → Producción, cableado contra la API de QA Guardian como quality gate. Con
`?download=true` se descarga como archivo `.yml`.

### Métricas de rendimiento (JMeter)

`JMeterTestRunner` parsea el JTL mapeando columnas por nombre y expone: TPS, tiempo
promedio/máximo/mínimo, **usuarios concurrentes** (máximo de `allThreads`), conteo y tasa de
errores. **Uso de CPU/memoria** del servidor bajo prueba: si el plan incluye un *PerfMon
Metrics Collector* que lee de un **ServerAgent** en la máquina objetivo, la plataforma parsea
su archivo de resultados (`PerfMonParser`) y reporta CPU promedio/máx (%) y memoria
promedio/máx (MB) en el nodo `resources` de las métricas. El ServerAgent es un componente
estándar de JMeter que se despliega en el servidor bajo prueba (inherente a cómo JMeter
captura recursos remotos); la ruta del archivo se indica con el parámetro `perfmonResults`
o por defecto `perfmon.jtl` en el directorio de la ejecución.

### Autoría y grabación de pruebas de Playwright

- **Editar / crear specs**: `POST /testcases/script` guarda el contenido de una spec (o
  collection/plan) y crea/actualiza el caso de prueba automatizado; `GET /testcases/{id}/script`
  devuelve el contenido para editarlo. El editor está disponible en la página de Casos de prueba.
- **Grabar**: `POST /testcases/record` con `{projectId, url}` ejecuta `playwright codegen`
  (`PlaywrightRecorder`). En una instalación local con entorno gráfico abre una sesión
  interactiva de grabación y devuelve la spec; en un servidor headless devuelve un **andamiaje
  inicial** para la URL (con instrucciones para grabar localmente). La spec resultante se edita
  y guarda con el editor.

### Validación de esquemas SQL Server

`SqlServerSchemaValidator` compara entre dos ambientes: tablas, columnas, índices, llaves
foráneas, procedimientos, triggers, conteo de registros y el **historial de migraciones EF
Core** (`__EFMigrationsHistory`, si el ambiente lo usa).

## 4. Agente IA

- `ClaudeAiAnalysisService` (diagnóstico de fallos): prompt con error/excepción, stacktrace,
  **extracto del log-evidencia** (leído del archivo adjunto), **consulta SQL** (extraída del
  error/stacktrace/métricas cuando es detectable) y ruta del screenshot →
  JSON estructurado `{diagnosis, probableCause, criticality, recommendation,
  suggestedPriority, estimatedHours, suggestedOwnerRole}` validado por
  `output_config.format` (json_schema). Los fallos con criticidad Alta/Crítica registran
  defecto automáticamente con el diagnóstico embebido. (El análisis de video no está
  soportado: el modelo no procesa video; el video se conserva como evidencia.)
- `ClaudeTestGenerationService` (agente de PR): archivos modificados → casos de prueba
  sugeridos (Playwright/Postman) que se guardan como borradores `TC-AI-xxxx`.
- Ambos degradan a heurísticas basadas en reglas si no hay API key o el LLM falla,
  y el campo `modelUsed` registra `heuristic-fallback` para trazabilidad.

## 5. Trabajos en segundo plano

Hangfire (`/hangfire`, solo Administrador): `TestExecutionJob.ExecuteAsync` (runs) y
`ValidateDatabaseAsync` (comparación de esquemas). Reintento automático: 1.
Storage: SQL Server (producción) o memoria (dev/test).

## 6. Extensión del sistema

1. **Nuevo runner**: clase que implemente `ITestRunner` (`Framework`, `ExecuteAsync`),
   registro scoped en `Infrastructure/DependencyInjection.cs` y mapeo en `TestRunnerFactory`.
2. **Nueva métrica de gate**: agregar valor a `GateMetric`, alimentarla en
   `ExecuteTestRunCommandHandler.EvaluateQualityGateAsync`.
3. **Nuevo evento de notificación**: flag en `NotificationEvents` y despacho donde ocurra.
4. Toda funcionalidad nueva debe llegar con sus pruebas; el pipeline CI la rechaza si
   rompe las existentes.

## 7. Convenciones de código

- Un agregado por transacción; los handlers modifican estado solo vía métodos de dominio.
- Errores de negocio → `DomainException` (HTTP 422); ausencias → `NotFoundException` (404);
  validación de entrada → FluentValidation (400 con diccionario de errores).
- Los servicios externos son *best-effort* cuando no bloquean el veredicto del gate
  (notificaciones, IA, checks de GitHub) y registran advertencias en Serilog.
