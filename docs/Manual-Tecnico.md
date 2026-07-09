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
| `/testruns` | `GET`, `POST` (encola ejecución), `POST {id}/cancel`, `GET {id}/report?format=Pdf|Excel|Word|Html|Json|Csv`, `GET evidence?path=` | Ejecución: Admin/QA/DevOps/LiderTecnico |
| `/defects` | `GET`, `POST`, `POST {id}/status` (workflow Nuevo→…→Cerrado) | Admin/QA/Desarrollador/LiderTecnico |
| `/qualitygates` | `GET`, `POST`, `POST assign` | Escritura: gestión de proyectos |
| `/dashboard` | `GET ?projectId=` KPIs, tendencia, errores por módulo | Autenticado |
| `/integrations` | `POST` upsert config · `GET sonarqube/{projectId}` · `GET github/{projectId}/pulls` · `POST github/{projectId}/pulls/{n}/analyze` · `POST database-validation` | Ver tabla de roles |
| `/admin/users`, `/admin/notification-channels` | Gestión de usuarios y canales | Administrador / gestión |

### Tiempo real (SignalR)

Hub: `/hubs/testruns` (JWT por query `access_token`). Métodos cliente→servidor:
`SubscribeToRun(runId)`, `UnsubscribeFromRun(runId)`. Eventos servidor→cliente:
`runStatusChanged {testRunId, status}`, `runCompleted {testRunId, passed, failed, gateStatus}`.

## 2. Configuración (appsettings / variables de entorno)

| Clave | Descripción |
|---|---|
| `ConnectionStrings:DefaultConnection` | SQL Server (o archivo SQLite si `Database:UseSqlite=true`) |
| `ConnectionStrings:Redis` | Vacío = caché en memoria |
| `Jwt:SigningKey/Issuer/Audience/AccessTokenMinutes/RefreshTokenDays` | Emisión y validación JWT |
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

## 4. Agente IA

- `ClaudeAiAnalysisService` (diagnóstico de fallos): prompt con error/stacktrace/logs →
  JSON estructurado `{diagnosis, probableCause, criticality, recommendation,
  suggestedPriority, estimatedHours, suggestedOwnerRole}` validado por
  `output_config.format` (json_schema). Los fallos con criticidad Alta/Crítica registran
  defecto automáticamente con el diagnóstico embebido.
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
