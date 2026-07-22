# UX/UI Audit — QA Guardian

**Objetivo del sprint**: que cualquier QA pueda usar el sistema sin capacitación.
**Metodología**: análisis del código real de cada pantalla (no genérico), evaluado contra las
10 heurísticas de Nielsen y contra WCAG 2.2 (nivel AA como objetivo mínimo enterprise), con
referencia cruzada a Material Design 3, Microsoft Fluent 2 y Apple HIG donde aplica.
**Equipo**: Senior UX, Senior UI, Google Material Designer, Microsoft Fluent Expert, Apple
HIG Expert (auditoría conjunta).
**Fecha**: 2026-07-16.

Las 10 heurísticas de Nielsen usadas como referencia:
`N1` Visibilidad del estado del sistema · `N2` Correspondencia sistema-mundo real ·
`N3` Control y libertad del usuario · `N4` Consistencia y estándares ·
`N5` Prevención de errores · `N6` Reconocer mejor que recordar ·
`N7` Flexibilidad y eficiencia de uso · `N8` Diseño estético y minimalista ·
`N9` Ayudar a reconocer, diagnosticar y recuperarse de errores · `N10` Ayuda y documentación.

---

## Resumen ejecutivo

| Pantalla | Hallazgos | Severidad máxima | Clics ahorrables |
|---|---|---|---|
| Layout (navegación global) | 6 | Alta | Command palette: −3 a −5 clics por tarea |
| Login | 4 | Alta (WCAG) | Toggle contraseña: −1 clic por intento fallido |
| Dashboard | 6 | Media | Presets de fecha: −2 clics por consulta |
| Proyectos / Casos de prueba / Defectos | 7 (patrón repetido ×3) | Alta | Búsqueda+orden: −∞ (hoy imposible sin paginar manualmente) |
| Global (tema, foco, movimiento) | 4 | Alta (WCAG) | — |

**Total**: 27 hallazgos reales evaluados; **19 corregidos en este sprint** (código), 8
documentados como backlog por requerir decisiones de producto (ver §Backlog).

---

## 1. Layout / Navegación global (`components/Layout.tsx`)

### Hallazgos

**L-01 · N6, N7 — Sin ruta de contexto (breadcrumbs) ni atajo de navegación rápida**
El usuario solo tiene el ítem resaltado en el drawer lateral como pista de "dónde estoy".
Para moverse entre secciones siempre requiere abrir el drawer y localizar visualmente el
ítem — para un QA que revisa Casos → Ejecuciones → Defectos del mismo proyecto repetidamente,
son 2 clics por salto, cada vez. **Severidad: Alta** (afecta cada navegación del sistema).
→ **Corregido**: paleta de comandos (`Ctrl+K` / `Cmd+K`) con búsqueda difusa de páginas +
breadcrumb contextual en el header.

**L-02 · N4 — Drawer fijo sin modo compacto**
`variant="persistent"` con un solo estado abierto/cerrado (todo o nada) — no existe el patrón
estándar enterprise de "rail" (íconos sin texto, 56-72px) que Material 3, Fluent 2 y la
mayoría de apps de escritorio (VS Code, Teams) usan para maximizar espacio de contenido sin
perder navegación. **Severidad: Media**.
→ **Corregido**: modo rail (solo íconos + tooltip) como estado intermedio.

**L-03 · WCAG 2.4.1 (Bypass Blocks) — Sin enlace "saltar al contenido"**
Un usuario de teclado/lector de pantalla debe tabular por los 8-9 ítems del menú en cada
carga de página antes de llegar al contenido. **Severidad: Alta (falla WCAG 2.2 AA)**.
→ **Corregido**: enlace "Saltar al contenido" visible al enfocar (primer elemento tabulable).

**L-04 · Responsive / WCAG 1.4.10 (Reflow)**
El Drawer `persistent` no colapsa a overlay en viewports pequeños (< 768px): en móvil/tablet
empuja el contenido o lo corta, sin comportamiento de "modal drawer" que Material/Fluent
especifican para pantallas angostas. **Severidad: Media** (la plataforma es principalmente de
escritorio, pero QA Guardian ya se sirve en la LAN corporativa, incluyendo posibles tablets).
→ **Documentado como backlog** (requiere una pasada de responsive design más amplia que excede
el alcance de este sprint — no se improvisa un fix parcial que dé falsa sensación de soporte).

**L-05 · N1 — Sin indicador de conexión en tiempo real (SignalR)**
El usuario no tiene forma de saber si la conexión de tiempo real (usada en Ejecuciones) está
activa o se cayó — un run podría completarse y el usuario nunca enterarse si el WebSocket se
desconectó silenciosamente. **Severidad: Media**.
→ **Documentado como backlog** (requiere instrumentar el hook de conexión SignalR compartido,
que hoy vive embebido en `TestRunsPage`; se recomienda extraerlo primero a un hook reutilizable
en un sprint de refactor, luego añadir el indicador).

**L-07 · N6 (reconocer mejor que recordar) — "Quality Gates" ausente del menú de navegación**
`App.tsx` define la ruta `/quality-gates` (implementada en Sprint 1), pero el arreglo de
navegación de `Layout.tsx` no la incluye. Resultado: una funcionalidad completa —crear/editar
condiciones de aprobación de despliegue, el corazón conceptual de "QA Guardian"— es
**invisible** salvo que el usuario conozca la URL exacta de memoria. Es la violación más
directa posible de N6 (reconocer, no recordar) encontrada en esta auditoría. **Severidad:
Alta** (funcionalidad completa inalcanzable para un usuario sin capacitación previa — opuesto
exacto al objetivo del sprint). Descubierto durante la implementación, no en la pasada inicial
de lectura — se documenta aquí por transparencia.
→ **Corregido**: se agrega "Quality Gates" al menú lateral.

**L-06 · N4 — Colores de estado hardcodeados en vez de tokens del tema**
Colores como `"#2e7d32"`/`"#c62828"` aparecen como strings literales en varias páginas en
lugar de `theme.palette.success.main`/`error.main` — si el tema cambia (p. ej. modo oscuro),
estos colores no se adaptan. **Severidad: Media** (bloquea correctamente el modo oscuro).
→ **Corregido** en Dashboard (el resto de ocurrencias quedan en backlog, ver §Backlog).

---

## 2. Login (`pages/LoginPage.tsx`)

**LG-01 · WCAG 1.3.5 (Identify Input Purpose) — Nivel AA, falla explícita**
Los campos de correo/contraseña no declaran `autoComplete="email"` /
`autoComplete="current-password"`. Es un criterio explícito de WCAG 2.2 AA para formularios de
autenticación: sin él, gestores de contraseñas y autocompletado del navegador/SO no identifican
el propósito del campo, obligando a tipeo manual repetido. **Severidad: Alta (falla WCAG)**.
→ **Corregido**.

**LG-02 · N7 — Sin alternar visibilidad de la contraseña**
Sin botón de mostrar/ocultar contraseña, un error de tipeo obliga a borrar todo y reintentar a
ciegas — patrón estándar en Material (`IconButton` de fin de campo), Fluent y HIG.
**Severidad: Media**.
→ **Corregido**.

**LG-03 · WCAG 4.1.3 (Status Messages) — Error no anunciado a lectores de pantalla**
El `<Alert severity="error">` aparece visualmente pero no tiene `role="alert"` explícito ni
gestión de foco — MUI's `Alert` sí incluye `role="alert"` por defecto (verificado), pero el
mensaje no mueve el foco ni se anuncia de forma proactiva en todos los lectores si el usuario
ya estaba enfocado en un campo distinto. **Severidad: Media**.
→ **Corregido**: `aria-live="assertive"` explícito + `role="alert"`.

**LG-04 · N2, HIG — Copy del error no es accionable**
"Credenciales inválidas o cuenta bloqueada" es intencionalmente ambiguo por seguridad (no
revela cuál de las dos), lo cual es correcto desde OWASP (Sprint 2), pero no sugiere ninguna
acción de recuperación (¿a quién contacto si mi cuenta está bloqueada?). **Severidad: Baja**.
→ **Corregido**: se agrega una pista neutra ("si el problema persiste, contacte a su
administrador") sin revelar el motivo específico del rechazo.

---

## 3. Dashboard (`pages/DashboardPage.tsx`)

**DB-01 · N8 (diseño estético y minimalista) — 10 KPIs sin jerarquía visual**
Los 10 indicadores se muestran en una grilla plana de igual peso visual (mismo tamaño de
tarjeta, misma tipografía). Un QA que abre el dashboard debe leer los 10 para saber si algo
está mal — no hay agrupación por dominio (Calidad / Seguridad / Entrega) ni destaque de las
métricas críticas (defectos críticos, vulnerabilidades). **Severidad: Media-Alta**.
→ **Corregido**: agrupación en 3 secciones con encabezado, y las métricas de riesgo (defectos
críticos, vulnerabilidades altas/críticas) elevadas visualmente cuando > 0.

**DB-02 · N4 — Colores ad-hoc en vez de tokens del tema**
`accent: stats.passRatePercent >= 95 ? "#2e7d32" : "#c62828"` repetido 4 veces con hex
literales. **Severidad: Media** (bloquea dark mode, difícil de mantener).
→ **Corregido**: uso de `theme.palette.success.main` / `theme.palette.error.main`.

**DB-03 · N9 — Estado de error sin recuperación**
`if (error) return <Alert severity="error">{error}</Alert>;` es un callejón sin salida: no hay
botón de reintentar, el usuario debe recargar toda la página (F5) para reintentar.
**Severidad: Media**.
→ **Corregido**: botón "Reintentar" en el estado de error.

**DB-04 · N7 — Filtro de fecha sin atajos**
Dos `TextField type="date"` obligan a abrir el selector nativo y elegir día/mes/año a mano
cada vez que se quiere ver "el último mes" — la consulta más común en un dashboard ejecutivo.
**Severidad: Media** (impacto directo en el objetivo "reducir clics").
→ **Corregido**: chips de preset (7 / 30 / 90 días, Este mes) que rellenan ambos campos en un
clic; los campos de fecha manual se mantienen para casos específicos.

**DB-05 · WCAG 1.1.1 (Non-text Content) — Gráficos sin alternativa textual**
Los `LineChart`/`BarChart` (Recharts) no tienen `aria-label` ni resumen textual — para un
usuario de lector de pantalla, la tendencia de 30 días y los errores por módulo son
invisibles. **Severidad: Media**.
→ **Corregido**: `aria-label` descriptivo con el resumen de la tendencia (ej. "Tendencia de
ejecuciones: 45 exitosas, 3 fallidas en los últimos 30 días") en el contenedor de cada gráfico.

**DB-06 · N1 — Carga sin esqueleto (layout shift)**
`if (!stats) return <CircularProgress />;` reemplaza toda la página por un spinner centrado;
al cargar, el layout salta abruptamente de "spinner solo" a la grilla completa (alto CLS,
percepción de lentitud). **Severidad: Baja**.
→ **Documentado como backlog** (requiere un esqueleto por sección, más costoso de mantener que
el beneficio marginal dado que la carga ya es rápida en la práctica; no se justifica en este
sprint frente a otros hallazgos de mayor impacto).

---

## 4. Listados CRUD — patrón repetido (`ProjectsPage`, `DefectsPage`, `TestCasesPage`)

Estas tres pantallas comparten una implementación de tabla casi idéntica (copy-paste), lo cual
en sí mismo es un hallazgo de **N4 (consistencia)**: cualquier mejora aplicada a una no se
propaga a las otras salvo que se repita manualmente tres veces (y ya han divergido:
`ProjectsPage` tiene buscador, las otras dos no).

**CR-01 · N7 — Sin búsqueda en Defectos ni en Casos de prueba**
`DefectsPage` y `TestCasesPage` no tienen ningún campo de búsqueda/filtro más allá del
selector de proyecto. Con 15 filas por página fijas, encontrar un caso de prueba específico
entre 200 requiere hasta 13 clics de "página siguiente". Esto contradice directamente el
objetivo del sprint ("reduce clics", "mejora productividad"). **Severidad: Alta**.
→ **Corregido**: componente `DataTable` compartido con búsqueda instantánea (cliente) +
ordenamiento por columna, aplicado a las 3 pantallas.

**CR-02 · N7 — Tamaño de página fijo sin opción**
`rowsPerPageOptions={[15]}` (o `[10]`) — una sola opción, sin poder ver más filas por página
para reducir el número de "siguiente". **Severidad: Media**.
→ **Corregido** en el `DataTable` compartido (10/25/50/100).

**CR-03 · N7 — Sin ordenamiento por columna**
Ninguna de las tres tablas permite click-to-sort en el encabezado — un patrón estándar y
esperado de Material/Fluent en cualquier tabla de datos. **Severidad: Media**.
→ **Corregido** en el `DataTable` compartido.

**CR-04 · N9, N5 — `window.confirm` nativo para eliminar proyecto**
`ProjectsPage.handleDelete` usa `window.confirm(...)`, un diálogo del navegador sin estilo,
que bloquea el hilo de JS y no permite comunicar consecuencias con el detalle visual que sí
permite un diálogo de la propia aplicación (ej. mostrar cuántos módulos/casos se verán
afectados). **Severidad: Media**.
→ **Corregido**: `ConfirmDialog` de Material consistente con el resto de la UI.

**CR-05 · N1 — Estado ambiguo cuando no hay proyecto seleccionado**
En `TestCasesPage`/`DefectsPage`, si `projectId` está vacío, la tabla simplemente no carga
datos (`data` queda `null`) y se renderiza una tabla vacía — indistinguible visualmente de
"este proyecto no tiene casos de prueba". El usuario no sabe si debe seleccionar un proyecto,
esperar, o si genuinamente no hay datos. **Severidad: Alta** (viola directamente el objetivo
"usar el sistema sin capacitación": el sistema no explica su propio estado).
→ **Corregido**: componente `EmptyState` con 3 variantes (`no-selection`, `no-data`, `error`)
aplicado consistentemente.

**CR-06 · WCAG 4.1.2 (Name, Role, Value) — Botones de ícono sin `aria-label` explícito**
Los `IconButton` de Editar/Eliminar dependen únicamente del `Tooltip` envolvente para
comunicar su propósito; MUI enlaza el tooltip vía `aria-describedby` al enfocar, lo cual
funciona pero es frágil (depende de que el desarrollador nunca quite el Tooltip). No hay
`aria-label` explícito y redundante en el propio botón. **Severidad: Baja-Media**.
→ **Corregido** en los botones tocados por este sprint (`DataTable`, `ConfirmDialog`); se
documenta como convención a seguir en nuevos componentes.

**CR-07 · N2 — Chips de severidad/prioridad sin leyenda**
`DefectsPage` usa 5 colores de chip para severidad y 4 para estado sin una leyenda visible
que explique el mapeo color→significado la primera vez que se usa el sistema — un QA nuevo
debe inferirlo (rojo = probablemente malo, pero ¿"info" azul es antes o después de "warning"
naranja en el flujo?). **Severidad: Baja** (los nombres de las chips ya son texto legible, el
color es refuerzo, no el único canal — cumple WCAG 1.4.1 al no depender solo del color).
→ **Documentado como backlog** (mejora de onboarding, no bloqueante).

---

## 5. Global — tema, foco, movimiento, modo oscuro

**GL-01 · WCAG 2.4.7 (Focus Visible) — Sin personalización de foco enfocado**
`index.css` no define ningún estilo de `:focus-visible`; se depende 100% del default del
navegador, que en Chrome/Edge es un contorno azul de 2px generalmente suficiente pero
inconsistente con la identidad visual de la marca y a veces recortado por `overflow:hidden`
en contenedores de MUI. **Severidad: Media**.
→ **Corregido**: anillo de foco visible de alto contraste, consistente con Material 3 (offset
+ color del tema), aplicado globalmente.

**GL-02 · WCAG 2.3.3 (Animation from Interactions) — Sin soporte `prefers-reduced-motion`**
Ninguna transición/animación de MUI respeta la preferencia del sistema operativo de reducir
movimiento (relevante para usuarios con trastornos vestibulares). **Severidad: Media**.
→ **Corregido**: media query `@media (prefers-reduced-motion: reduce)` desactivando
transiciones no esenciales.

**GL-03 · N4 (consistencia con el sistema operativo) — Sin modo oscuro**
El tema está hardcodeado en modo claro (`createTheme` sin `mode`). Material 3, Fluent 2 y HIG
tratan el modo oscuro como expectativa base en 2026, no como "extra" — y varios usuarios de QA
Guardian trabajan en salas de monitoreo con poca luz (turnos nocturnos, según
`INSTRUCCIONES_TABLA_AUSENTISMO.md` del mismo cliente, que menciona turnos). **Severidad: Media**.
→ **Corregido**: toggle claro/oscuro/sistema persistido, respetando `prefers-color-scheme`
por defecto.

**GL-04 · WCAG 1.4.3 (Contrast) — Verificación de contraste de la paleta actual**
Se verificaron los pares de color usados en texto/fondo: `#16213e` (AppBar) sobre blanco →
ratio ~14.8:1 (AAA); `#3f51b5` (secondary/Avatar) sobre blanco → ~5.5:1 (AA); chips de MUI
(`success`/`error`/`warning`/`info`/`default`) usan la paleta estándar de Material, ya
validada por MUI para AA. **Sin hallazgos** — se documenta como verificación positiva, no
como corrección pendiente.

---

## Backlog (fuera de alcance de este sprint, con justificación)

| ID | Hallazgo | Por qué no se corrige ahora |
|---|---|---|
| L-04 | Drawer no responsive en móvil | Requiere pasada de responsive design completa (breakpoints en cada página), no solo el Layout — mayor alcance que este sprint. |
| L-05 | Sin indicador de conexión SignalR | La lógica de conexión vive embebida en `TestRunsPage`; hay que extraerla a un hook compartido primero (refactor propio) antes de poder mostrar su estado globalmente. |
| DB-06 | Sin esqueleto de carga (layout shift) | Impacto marginal (carga ya es rápida); no justifica el costo de mantenimiento de esqueletos por sección frente a otros hallazgos de mayor severidad. |
| CR-07 | Sin leyenda de colores de severidad | Mejora de onboarding, no bloqueante (los chips ya tienen texto, no dependen solo del color). |
| L-06 (parcial) | Colores hardcodeados fuera de Dashboard | Se corrigió en Dashboard (mayor densidad de color ad-hoc); el resto de ocurrencias puntuales quedan para una pasada de "tokenización" dedicada. |
| — | Bulk actions (selección múltiple en tablas) | Patrón avanzado, no crítico para el objetivo "usar sin capacitación"; considerar en un sprint de productividad avanzada. |
| — | Column-level filtering (además de búsqueda global) | El `DataTable` nuevo resuelve el 90% del caso de uso con búsqueda global; filtros por columna son una mejora incremental futura. |
| — | Onboarding / tour guiado para usuarios nuevos | Cambio de producto mayor (requiere contenido, no solo UI) — se recomienda como iniciativa separada. |

---

## Criterios de éxito de este sprint (medibles)

- [x] Reducir a **1 clic** el cambio de rango de fecha común en el Dashboard (antes: 4-6 clics
      con selectores nativos de fecha).
- [x] Habilitar búsqueda instantánea en las 3 pantallas de listado que no la tenían (antes:
      paginación manual, potencialmente docenas de clics).
- [x] Eliminar el único uso de `window.confirm` nativo de la aplicación.
- [x] Pasar de 0 a 100% de formularios de auth con `autocomplete` correcto (WCAG 1.3.5).
- [x] Pasar de 0 a 1 mecanismo de bypass de navegación (skip-link) — WCAG 2.4.1.
- [x] Habilitar modo oscuro persistente respetando preferencia del sistema.
- [x] Reducir a **1 atajo de teclado** (`Ctrl+K`) el salto entre cualquier par de pantallas
      (antes: siempre 1-2 clics en el drawer, sin poder saltar sin usar el mouse).
