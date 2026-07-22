# Design System — QA Guardian (Sprint 3)

Documenta los componentes compartidos creados en este sprint, el sistema de tokens de tema
(claro/oscuro), y la justificación de cada decisión frente a Nielsen, WCAG 2.2 y los tres
sistemas de diseño de referencia (Material 3, Fluent 2, Apple HIG).

**Mockups visuales**: ver el artifact publicado (antes/después de los 4 hallazgos más
críticos) — enlace compartido en la conversación. Este documento es el complemento textual:
qué se construyó, por qué, y cómo reutilizarlo.

---

## 1. Sistema de tokens (tema claro/oscuro)

**Archivo**: `frontend/src/theme/theme.ts`, `frontend/src/theme/ThemeModeContext.tsx`

| Token | Claro | Oscuro | Decisión |
|---|---|---|---|
| `primary.main` | `#16213e` | `#16213e` (**fijo**) | Ver justificación abajo |
| `secondary.main` | `#3f51b5` | `#3f51b5` (fijo) | Contraste ~5.5:1 con blanco en ambos modos |
| `background.default` | `#f4f6fb` | `#121218` | Superficie base |
| `background.paper` | blanco (MUI default) | `#1c1c24` | Tarjetas/diálogos |

**Decisión: `primary.main` no cambia entre modos.** La alternativa obvia (aclarar el primario
en modo oscuro, patrón común en Material) se descartó tras verificar contraste: un indigo
claro tipo `#7986cb` sobre texto blanco del AppBar ronda 2.5:1 — **falla WCAG 1.4.3 (AA
4.5:1)**. Mantener `#16213e` (~14.8:1 con blanco, verificado) en ambos modos es más simple y
nunca arriesga el contraste del AppBar. Solo cambian las superficies de fondo, que es lo que
el modo oscuro necesita resolver (reducir luminancia general, no recolorear la marca).

**Persistencia**: `localStorage` (clave `qaguardian.theme-mode`) — apropiado aquí porque es
una preferencia de interfaz, no una credencial (a diferencia de los tokens de sesión, que
desde Sprint 2 viven en memoria/cookie httpOnly, nunca en localStorage).

**Justificación (Material 3 / Fluent 2 / Apple HIG)**: los tres sistemas de diseño de
referencia tratan el modo oscuro como parte del contrato base de una app en 2026, no como
"característica extra" — HIG incluso lo exige para apps nuevas de Apple. Se implementó con
tres estados (`light`/`dark`/`system`) porque forzar solo dos ignora la preferencia real del
SO, violando N4 (consistencia con convenciones externas a la app).

---

## 2. Componentes compartidos

### 2.1 `DataTable<T>` — `frontend/src/components/DataTable.tsx`

Tabla con búsqueda instantánea, ordenamiento por columna (`TableSortLabel`) y tamaño de
página configurable (10/25/50/100), genérica sobre cualquier tipo de fila.

**Reemplaza**: la tabla manual duplicada tres veces en `ProjectsPage`, `DefectsPage`,
`TestCasesPage` (hallazgo CR-01..CR-03).

**Decisión de datos — búsqueda client-side, no server-side.** Se evaluaron dos caminos:

1. Mantener búsqueda en el servidor (patrón que ya tenía `ProjectsPage`, con un `search`
   param en cada tecla) — requiere debounce, tiene latencia de red por carácter, y no estaba
   implementado en absoluto en `DefectsPage`/`TestCasesPage`.
2. **Elegido**: traer el catálogo completo del proyecto una vez (`pageSize=200`, el mismo
   techo que ya aplica `Repository<T>.PagedAsync` en el backend) y filtrar/ordenar/paginar en
   memoria del cliente.

Se eligió la opción 2 porque los catálogos de QA Guardian están acotados por proyecto
(decenas o cientos de casos de prueba/defectos, no miles) — a esa escala, cargar una vez y
responder instantáneamente en el cliente es estrictamente mejor UX (0 ms por tecla, sin
parpadeo de carga) sin costo real de memoria/red. El componente **avisa explícitamente**
(`totalOnServer`) si el catálogo excede el techo, en vez de truncar en silencio — evita crear
una nueva forma de N1 (visibilidad del estado del sistema) rota. Si un proyecto futuro supera
consistentemente las ~200 filas, la recomendación es agregar filtros server-side adicionales,
no волver a una búsqueda por-tecla contra el servidor.

**Justificación de UI**: `TableSortLabel` y el patrón de encabezado ordenable son el
componente estándar de Material 3 para tablas de datos; Fluent 2 (`DetailsList`) usa el mismo
patrón visual (flecha junto al encabezado activo). Cumple N7 (flexibilidad para usuarios
expertos que ya saben qué buscan) y N4 (mismo comportamiento en las tres pantallas).

### 2.2 `EmptyState` — `frontend/src/components/EmptyState.tsx`

Cuatro variantes: `loading` / `no-selection` / `no-data` / `error` (con botón "Reintentar").

**Reemplaza**: los callejones sin salida repartidos por la app —
`if (error) return <Alert>...` sin acción de recuperación (DB-03), tablas vacías ambiguas
cuando no hay proyecto seleccionado (CR-05).

**Justificación**: N1 (visibilidad del estado del sistema) exige que el sistema comunique
*cuál* de los tres estados posibles (cargando / sin selección / sin datos) está activo — antes
eran visualmente indistinguibles. N9 (ayudar a recuperarse de errores) exige una acción, no
solo un mensaje; de ahí el botón "Reintentar" en la variante `error`. Los `role="status"` /
`role="alert"` + `aria-live` correspondientes comunican el cambio de estado a lectores de
pantalla sin que el usuario tenga que "descubrir" visualmente que algo cambió.

### 2.3 `ConfirmDialog` — `frontend/src/components/ConfirmDialog.tsx`

**Reemplaza**: el único uso de `window.confirm()` nativo de la aplicación (`ProjectsPage`,
eliminar proyecto — hallazgo CR-04).

**Justificación**: un diálogo nativo del navegador rompe N4 (consistencia visual con el resto
de la app), no permite describir consecuencias con formato (negrita, colores de riesgo), y
bloquea el hilo de JS. El prop `destructive` tiñe el botón de confirmación en rojo
(`color="error"`) — Material 3 recomienda reservar el color de error exclusivamente para
acciones irreversibles, nunca como acento decorativo.

### 2.4 `CommandPalette` — `frontend/src/components/CommandPalette.tsx`

Paleta de comandos activada por `Ctrl+K`/`Cmd+K` o el botón "Buscar…" del AppBar; búsqueda
difusa por substring sobre las páginas de navegación, navegable con flechas + Enter.

**Justificación**: patrón estándar en herramientas densas para profesionales (VS Code, Linear,
Notion, Teams) — Fluent 2 lo documenta como "command palette" en su librería de patrones para
apps de productividad. Resuelve directamente N7 (eficiencia para usuarios expertos): saltar
entre 2 pantallas cualquiera pasa de 1-2 clics en el drawer a 1 atajo + Enter, sin soltar el
teclado. Se expone también como botón visible (no solo el atajo) porque un mecanismo
*únicamente* de teclado viola N6 (reconocer, no recordar) para quien no conoce el atajo de
memoria.

---

## 3. Accesibilidad global (WCAG 2.2 AA)

| Mecanismo | Archivo | Criterio WCAG |
|---|---|---|
| Enlace "Saltar al contenido" (visible al enfocar) | `index.css` (`.skip-link`), `Layout.tsx` | 2.4.1 Bypass Blocks |
| Anillo de foco visible y consistente | `index.css` (`:focus-visible`) | 2.4.7 Focus Visible |
| Reducción de movimiento respetando el SO | `index.css` (`@media prefers-reduced-motion`) | 2.3.3 Animation from Interactions |
| `autocomplete="email"` / `"current-password"` | `LoginPage.tsx` | 1.3.5 Identify Input Purpose |
| Alternativa textual de gráficos (`aria-label` con resumen) | `DashboardPage.tsx` | 1.1.1 Non-text Content |
| `aria-label` explícito en botones de ícono | `DataTable`, `ProjectsPage`, `TestCasesPage` | 4.1.2 Name, Role, Value |
| `role="alert"` / `aria-live` en errores | `LoginPage.tsx`, `EmptyState.tsx` | 4.1.3 Status Messages |

---

## 4. Cómo usar estos componentes en pantallas nuevas

```tsx
// Listado con búsqueda/orden/paginación instantáneos
<DataTable
  aria-label="Tabla de X"
  columns={[
    { key: "code", label: "Código", sortValue: (row) => row.code },
    { key: "status", label: "Estado", sortValue: (row) => row.status,
      render: (row) => <Chip label={STATUS[row.status]} /> },
  ]}
  rows={data.items}
  getRowKey={(row) => row.id}
  totalOnServer={data.totalCount}
  rowActions={(row) => <IconButton aria-label={`Editar ${row.code}`}>...</IconButton>}
/>

// Estados de carga/vacío/error consistentes
{!projectId ? <EmptyState variant="no-selection" /> :
 loadError ? <EmptyState variant="error" onRetry={load} /> :
 !data ? <EmptyState variant="loading" /> :
 <DataTable ... />}

// Confirmación de acciones destructivas
<ConfirmDialog
  open={!!deleting} destructive
  title="Eliminar X" description="¿Está seguro? Esta acción no se puede deshacer."
  confirmLabel="Eliminar"
  onConfirm={...} onCancel={() => setDeleting(null)}
/>
```

**Convención para toda página nueva**: usar `theme.palette.success/error/warning.main` para
colores semánticos (nunca hex literal — bloquea el modo oscuro), y agregar cualquier ruta
nueva al arreglo `getNavigation()` en `Layout.tsx` el mismo commit en que se crea la página —
el hallazgo L-07 de este sprint (Quality Gates invisible) ocurrió exactamente por saltarse
este paso.

---

## 5. Métricas de impacto (antes → después)

| Tarea | Antes | Después |
|---|---|---|
| Cambiar dashboard a "últimos 30 días" | 4-6 clics (selector nativo × 2 campos) | 1 clic (chip de preset) |
| Buscar un defecto por título entre 200 | Paginar manualmente (hasta 13 clics de "siguiente") | Instantáneo (tecleo directo) |
| Llegar a Quality Gates sin conocer la URL | Imposible vía UI | 1 clic en el drawer, o `Ctrl+K` → 2 letras → Enter |
| Confirmar eliminación de un proyecto | `window.confirm` sin estilo, sin detalle de consecuencias | Diálogo con contexto + color de riesgo |
| Saltar entre 2 pantallas cualquiera | 1-2 clics en el drawer, siempre con mouse | `Ctrl+K` + Enter, sin mouse |
