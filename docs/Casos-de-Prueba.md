# Casos de Prueba — Suite de aceptación de QA Guardian

Suite que valida la propia plataforma. Los casos marcados ✅ están automatizados en
`tests/` (xUnit) y corren en cada pipeline; los demás son de aceptación manual/E2E.

## Módulo: Autenticación y seguridad

| ID | Caso | Pasos | Resultado esperado | Automatizado |
|---|---|---|---|---|
| AUTH-01 | Login válido | Ingresar credenciales correctas | Tokens emitidos, roles presentes | ✅ unit + integración |
| AUTH-02 | Login inválido | Contraseña errónea | 401 con mensaje genérico (no revela si el correo existe) | ✅ |
| AUTH-03 | Bloqueo por fuerza bruta | 5 intentos fallidos | Cuenta bloqueada 15 min | ✅ unit |
| AUTH-04 | Rotación de refresh token | Usar `/auth/refresh` | Token anterior revocado, nuevo emitido | ✅ dominio |
| AUTH-05 | Endpoint protegido sin token | GET `/projects` sin Bearer | 401 | ✅ integración |
| AUTH-06 | RBAC | Usuario rol Cliente intenta crear proyecto | 403 | Manual |
| AUTH-07 | Rate limiting de login | >10 logins/min | 429 | Manual |

## Módulo: Proyectos y casos de prueba

| ID | Caso | Resultado esperado | Automatizado |
|---|---|---|---|
| PROJ-01 | Crear proyecto | Se asigna el gate por defecto automáticamente | ✅ unit + integración |
| PROJ-02 | Código duplicado | Rechazado con mensaje claro | ✅ unit |
| PROJ-03 | Módulos duplicados en un proyecto | `DomainException` | ✅ unit |
| TC-01 | Crear caso con pasos ordenados | Pasos persistidos en orden | ✅ integración |
| TC-02 | Pasos con orden duplicado | Rechazado | ✅ unit |
| TC-03 | Automatizar caso (framework + script) | Estado pasa a Activo/Automatizado | ✅ unit |
| TC-04 | Validación de payload inválido | 400 con diccionario de errores | ✅ integración |

## Módulo: Ejecuciones y Quality Gate

| ID | Caso | Resultado esperado | Automatizado |
|---|---|---|---|
| RUN-01 | Ciclo de vida del run | Pendiente→EnCurso→Completada con fechas | ✅ unit |
| RUN-02 | PassRate calculado | 2✅+1❌+1⏭️ → 50 % | ✅ unit |
| RUN-03 | Gate aprueba con métricas en umbral | `Passed`, despliegue aprobado | ✅ unit |
| RUN-04 | Gate rechaza condición bloqueante | `Failed`, despliegue bloqueado | ✅ unit |
| RUN-05 | Condición no bloqueante incumplida | `Warning`, despliegue aprobado | ✅ unit |
| RUN-06 | Métrica ausente | Tratada como incumplida | ✅ unit |
| RUN-07 | Evidencia adjunta a resultado fallido | Screenshot/video/log descargables | ✅ unit (dominio) + manual E2E |
| RUN-08 | Reporte PDF/Excel/Word/HTML/JSON/CSV | Archivo válido con KPIs y detalle | Manual |
| RUN-09 | Progreso en tiempo real | Frontend recibe `runCompleted` vía SignalR | Manual |

## Módulo: Defectos

| ID | Caso | Resultado esperado | Automatizado |
|---|---|---|---|
| DEF-01 | Workflow completo Nuevo→Cerrado | Transiciones válidas con fechas | ✅ unit |
| DEF-02 | Transición inválida (verificar sin resolver) | `DomainException` | ✅ unit |
| DEF-03 | Reapertura limpia fechas | `ResolvedAt` en null | ✅ unit |
| DEF-04 | Defecto automático por fallo crítico | Creado con diagnóstico IA y notificación | Manual (requiere run real) |

## Módulo: Integraciones e IA

| ID | Caso | Resultado esperado | Automatizado |
|---|---|---|---|
| INT-01 | Métricas SonarQube | Coverage/duplicación/bugs/hotspots visibles | Manual (requiere Sonar) |
| INT-02 | PRs de GitHub listados | Número, autor, ramas y URL | Manual (requiere repo) |
| INT-03 | Agente de PR | Casos `TC-AI-xxxx` creados + smoke run + comentario en PR | Manual |
| INT-04 | IA sin API key | Diagnóstico heurístico, `modelUsed=heuristic-fallback` | ✅ implícito en integración |
| INT-05 | Escaneo ZAP | Hallazgos categorizados (SQLi/XSS/CSRF/Headers/Cookies) con riesgo | Manual |
| INT-06 | Validación de esquemas SQL | Diferencias por tablas/columnas/índices/llaves/SPs/triggers | Manual (requiere 2 BD) |
| INT-07 | Notificación multicanal | Mensaje llega al canal suscrito al evento | Manual |

## Módulo: Pipeline CI/CD

| ID | Caso | Resultado esperado |
|---|---|---|
| CI-01 | Push a main | Build + 38 pruebas + build frontend + imágenes Docker |
| CI-02 | Gate rechaza en pipeline cliente | Job `qa-guardian-gate` falla y detiene el deploy |
| CI-03 | Smoke en staging falla | No se despliega a producción |
| CI-04 | Todo verde | Despliegue a producción tras aprobación del ambiente |
