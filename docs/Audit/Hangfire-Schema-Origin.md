# Hangfire Schema Origin (B5)

Documento de trazabilidad para auditorías: origen del script SQL que crea el
esquema `[HangFire]` **fuera** del runtime del API (`PrepareSchemaIfNecessary=false`).

| Campo | Valor |
|-------|--------|
| **Versión Hangfire** | Hangfire.SqlServer / Hangfire.Core / Hangfire.NetCore **1.8.18** (paquete NuGet en `QAGuardian.Infrastructure.csproj`) |
| **Versión de esquema** | Schema v9 (Install.sql de Hangfire.SqlServer 1.8.18) |
| **Fecha de incorporación** | 2026-07-21 |
| **Archivo versionado** | `database/05-hangfire-schema.sql` |
| **Fuente del script** | `Install.sql` oficial del paquete NuGet **Hangfire.SqlServer 1.8.18** (contenido extraído del artefacto publicado en nuget.org; no regenerado a mano) |
| **SHA256** | `9760050606f266a0d6830416d0617973d048c975701f407d44f3587d691902e1` |
| **Commit de incorporación** | `c598db7` — `feat(security): Hangfire schema bootstrap off-runtime (B5)` (PR #9) |
| **Responsable de incorporación** | Edilson Andrés Alvarez Jaramillo (stack seguridad B5 / Sprint 20-A) |
| **Scripts asociados** | `database/00-app-user-hangfire.sql` (grants DML al app user); orden compose `db-init`: schema → `00-app-user.sql` → grants |

## Política de actualización

1. **No editar** `05-hangfire-schema.sql` a mano salvo corrección de encoding/BOM o
   variables (`HangFireSchema`) documentadas.
2. Ante **upgrade** de `Hangfire.SqlServer` (p. ej. 1.8.18 → 1.x/2.x):
   - Extraer el nuevo `Install.sql` del paquete NuGet correspondiente.
   - Sustituir `database/05-hangfire-schema.sql`.
   - Recalcular y actualizar el **SHA256** y la **versión** en este documento.
   - Ejecutar `db-init` (o sqlcmd equivalente) en cada ambiente **antes** de
     desplegar el API con el nuevo paquete.
   - Verificar que `Hangfire:PrepareSchemaIfNecessary` sigue en `false` fuera de
     Development.
3. El API **no** debe crear ni alterar el esquema Hangfire en Production/QA/Staging.
4. Cualquier desviación del hash SHA256 anterior debe quedar registrada en el PR
   que actualice el script (diff + este archivo).

## Verificación rápida del hash

```powershell
certutil -hashfile database\05-hangfire-schema.sql SHA256
```

El valor debe coincidir exactamente con la fila **SHA256** de la tabla superior.
