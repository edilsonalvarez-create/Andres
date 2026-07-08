# Manual de Usuario — QA Guardian

## 1. Ingreso

Abra la URL de la plataforma, ingrese su correo y contraseña. Tras 5 intentos fallidos la
cuenta se bloquea 15 minutos. Su sesión dura 30 minutos y se renueva automáticamente.

## 2. Roles y qué puede hacer cada uno

| Rol | Capacidades |
|---|---|
| **Administrador** | Todo: usuarios, roles, canales, Hangfire |
| **QA** | Proyectos, casos de prueba, ejecuciones, defectos |
| **Líder Técnico** | Lo de QA + quality gates y aprobaciones |
| **DevOps** | Ejecuciones, pipelines, validación de BD |
| **Desarrollador** | Consulta resultados, gestiona sus defectos |
| **Product Owner / Cliente / Auditor** | Dashboards, reportes y evidencias (solo lectura) |

## 3. Dashboard ejecutivo

La pantalla inicial muestra: proyectos activos, casos de prueba, **cobertura de
automatización**, ejecuciones de los últimos 30 días, % de éxito, defectos abiertos y
críticos, vulnerabilidades altas/críticas e **índice de calidad** (0-100, pondera éxito,
defectos críticos y vulnerabilidades). Incluye la **tendencia** diaria de pruebas
exitosas/fallidas y el gráfico de **errores por módulo** para focalizar esfuerzos.

## 4. Proyectos

**Proyectos → Nuevo proyecto**: código único (p. ej. `ERP`), nombre, descripción y URL del
repositorio. El proyecto queda protegido automáticamente por el **quality gate por defecto**
(éxito ≥ 95 %, 0 vulnerabilidades críticas). Los módulos se agregan desde la API o al
importar requerimientos.

## 5. Casos de prueba

**Casos de prueba**: seleccione el proyecto para ver el inventario con tipo, prioridad y
estado de automatización. Un caso nace **Manual** con sus pasos (acción → resultado
esperado); al vincularle un script (spec de Playwright, collection de Postman, plan de
JMeter o URL para ZAP) pasa a **Automatizado** y se incluye en las ejecuciones de su tipo.

## 6. Ejecuciones

**Ejecuciones → Nueva ejecución**: elija tipo (Funcional, Regresión, API, Rendimiento,
Seguridad, Smoke…) y ambiente. La ejecución corre en segundo plano y la pantalla se
actualiza **en tiempo real**. Al finalizar verá total/exitosas/fallidas, % de éxito y el
veredicto del **Quality Gate**:

- 🟢 **Passed** — el despliegue queda aprobado.
- 🟡 **Warning** — pasa, pero hay condiciones no bloqueantes incumplidas.
- 🔴 **Failed** — **el despliegue queda bloqueado** (y el PR no se puede mergear si el
  repositorio tiene la protección de rama activada).

Cada resultado fallido incluye evidencias (screenshot, video, log) y el **diagnóstico del
agente IA**: causa probable, criticidad, recomendación, prioridad sugerida, horas estimadas
y rol responsable sugerido. Los fallos críticos generan defecto automáticamente.

Descargue el **reporte** de cualquier ejecución completada en PDF, Excel, Word, HTML,
JSON o CSV desde la misma tabla.

## 7. Defectos

**Defectos**: bandeja por proyecto con severidad, prioridad y estado. Flujo de vida:
`Nuevo → Asignado → En progreso → Resuelto → Verificado → Cerrado` (con `Reabierto` y
`Rechazado`). Los defectos `[Auto]` los creó el agente IA a partir de fallos críticos e
incluyen el diagnóstico completo en la descripción.

## 8. Integraciones

**Integraciones**: seleccione el proyecto para ver:

- **SonarQube**: cobertura, duplicación, bugs, vulnerabilidades, security hotspots,
  code smells y estado del gate de Sonar.
- **GitHub**: Pull Requests abiertos. El botón **“Analizar con IA”** ejecuta el agente
  inteligente: detecta los archivos modificados, genera casos de prueba sugeridos,
  dispara los Smoke Tests del proyecto y comenta el resumen en el propio PR.

## 9. Notificaciones

La plataforma avisa automáticamente por **correo, Microsoft Teams, Slack, Discord o
Telegram** cuando: una prueba falla, aparece una vulnerabilidad alta/crítica, un
despliegue es rechazado por el gate, o se crea un defecto automático. Los canales los
configura un administrador (globales o por proyecto).

## 10. Preguntas frecuentes

**¿Por qué mi ejecución quedó “Completada” con 0 pruebas?** El proyecto no tiene casos
automatizados activos de ese tipo; automatice casos primero.

**¿Por qué se bloqueó mi despliegue si todas las pruebas pasaron?** Revise el detalle del
gate: puede haber vulnerabilidades críticas detectadas por el escaneo de seguridad.

**¿Puedo cambiar los umbrales del gate?** Sí — un Líder Técnico o Administrador puede crear
gates personalizados y asignarlos por proyecto.
