# Regression Tests — Sprint 2 (Seguridad)

Documenta las pruebas automatizadas agregadas para fijar el comportamiento de cada
corrección de seguridad, más las pruebas manuales que cubren lo que no es viable
automatizar a nivel unitario (cookies, CSRF, CSP — cubiertas por
[Pentest-Checklist.md](Pentest-Checklist.md)).

## Cómo ejecutar

```bash
dotnet test QAGuardian.sln
# Esperado: 98 pruebas, 0 fallidas (82 preexistentes + 16 de este sprint)

cd frontend && npx tsc -b --noEmit && npm run build
# Esperado: 0 errores de tipo, build de producción exitoso
```

---

## Tests automatizados agregados

### `tests/QAGuardian.UnitTests/Infrastructure/JwtTokenServiceTests.cs`
Cubre SEC-06 (Threat-Model TM-06): longitud mínima de la clave de firma HS256.

| Test | Qué fija |
|---|---|
| `Rechaza_una_clave_de_firma_demasiado_corta` | Clave < 32 bytes → `InvalidOperationException` |
| `Rechaza_una_clave_vacia` | Clave vacía sigue rechazada (comportamiento preexistente) |
| `Acepta_una_clave_de_32_bytes_exactos` | Límite exacto (32 bytes) no debe fallar |
| `Genera_un_access_token_valido_con_clave_suficiente` | El token generado tiene forma JWT válida (3 segmentos) |
| `Los_refresh_tokens_generados_son_unicos` | Sin regresión en la generación de refresh tokens |

### `tests/QAGuardian.UnitTests/Infrastructure/AesTokenEncryptionServiceTests.cs`
Cubre SEC-05 (Threat-Model TM-05): migración de AES-CBC a AES-GCM.

| Test | Qué fija |
|---|---|
| `Encrypt_seguido_de_Decrypt_devuelve_el_texto_original` | Round-trip correcto (contrato base) |
| `Dos_cifrados_del_mismo_texto_producen_salidas_distintas` | Nonce aleatorio por operación (nunca reutilizado) |
| `Rechaza_un_ciphertext_manipulado_un_solo_bit` | **La prueba clave**: GCM detecta manipulación que CBC-sin-HMAC no detectaba — si alguien revierte a CBC por error, este test falla |
| `Decrypt_de_un_texto_truncado_lanza_excepcion_controlada` | Entrada corrupta no causa excepción no controlada |
| `Claves_distintas_no_pueden_descifrar_entre_si` | Aislamiento de claves entre configuraciones |

### `tests/QAGuardian.UnitTests/Application/Auth/LogoutCommandTests.cs`
Cubre SEC-03 (Threat-Model TM-03): logout real con revocación server-side.

| Test | Qué fija |
|---|---|
| `Revoca_un_refresh_token_activo` | El logout efectivamente marca el token como inactivo y persiste |
| `Es_idempotente_si_el_token_no_existe` | Logout sin sesión no lanza error ni revela información |
| `Es_idempotente_si_el_token_ya_estaba_revocado` | Doble logout no falla ni duplica escrituras |

### `tests/QAGuardian.UnitTests/Infrastructure/DbInitializerSecurityTests.cs`
Cubre SEC-01 (Threat-Model TM-08): defensa en profundidad contra el password público conocido.

| Test | Qué fija |
|---|---|
| `Rechaza_la_contrasena_publica_conocida_de_versiones_anteriores` | `DbInitializer` rechaza `"QaGuardian.2026!"` incluso si `Program.cs` no filtrara |
| `Acepta_una_contrasena_unica_y_crea_el_admin` | El flujo normal (contraseña válida) sigue funcionando |
| `No_duplica_el_admin_si_ya_existe` | Idempotencia de la siembra (comportamiento preexistente, sin regresión) |

**Nota de infraestructura de test**: usa SQLite en modo `:memory:` con una conexión abierta
durante todo el test (patrón estándar de EF Core para pruebas sin depender de un archivo en
disco ni de SQL Server).

---

## Cobertura NO automatizada (requiere ejecución manual — ver Pentest-Checklist.md)

Estas propiedades dependen de comportamiento de cookies/HTTP real (`Set-Cookie`,
`SameSite`, CSP aplicado por el navegador) que `WebApplicationFactory` en memoria no
reproduce con fidelidad suficiente para aserciones automatizadas confiables en este sprint:

| Propiedad | Referencia |
|---|---|
| El refresh token no aparece en el JSON de `/auth/login` | PT-04 |
| La cookie `qaguardian_rt` tiene `HttpOnly`+`Secure`+`SameSite=Strict` | PT-05 |
| `document.cookie` no expone `qaguardian_rt` en el navegador | PT-06 |
| `/auth/refresh` sin `X-CSRF-Token` es rechazado | PT-07 |
| `/auth/logout` revoca de verdad (reintentar refresh falla) | PT-08 |
| Recargar la página (F5) restaura la sesión vía silent refresh | PT-09 |
| Las 7 cabeceras de seguridad están presentes en API y nginx | PT-10 |
| CSP bloquea `onerror` inline inyectado en runtime | PT-11 |
| Clickjacking bloqueado (`X-Frame-Options`/`frame-ancestors`) | PT-12 |
| El hub de SignalR rechaza runs inexistentes | PT-13 |
| `access_token` de SignalR no persiste en logs de nginx | PT-15 |

**Recomendación de seguimiento**: si el equipo adopta Playwright para E2E (ya está en el
Product Backlog de otro sprint), migrar PT-04..PT-12 a specs automatizadas que sí levanten
un navegador real y puedan inspeccionar cookies/CSP/consola — hoy quedan como checklist manual.

---

## Estado de la suite de integración HTTP

`tests/QAGuardian.IntegrationTests/ApiIntegrationTests.cs` ya cubre (sin cambios en este
sprint, siguen aplicando):
- `Login_con_credenciales_invalidas_devuelve_401`
- `Endpoints_protegidos_requieren_token`

Estas pruebas **no requirieron modificación** porque solo verifican `accessToken` en la
respuesta de login (nunca dependieron de `refreshToken`, que es justamente el campo que se
removió del JSON). **No se pudieron re-ejecutar en esta sesión** por un proceso
`QAGuardian.API` que ya estaba corriendo en background (bloqueo de archivo de compilación,
detallado en Security-Report.md, sección Verificación). Ejecutar
`dotnet test tests/QAGuardian.IntegrationTests` tras detener ese proceso, antes de mergear.

---

## Checklist de regresión antes de release

- [ ] `dotnet test QAGuardian.sln` → 98/98 (o más, si se agregaron tests nuevos)
- [ ] `dotnet test tests/QAGuardian.IntegrationTests` → sin fallos (requiere que no haya un
      proceso previo bloqueando el build)
- [ ] `npx tsc -b --noEmit` (frontend) → 0 errores
- [ ] `npm run build` (frontend) → build exitoso
- [ ] Ejecutar Bloque A completo de Pentest-Checklist.md (autenticación/sesión) contra QA
- [ ] Ejecutar Bloque B (cabeceras) contra QA y contra la build de producción del frontend
      (nginx) — la CSP de la API y la de nginx deben coincidir
- [ ] Confirmar que ninguna integración configurada antes de este release quedó con tokens
      cifrados en el formato AES-CBC anterior (re-guardar cada integración una vez tras
      desplegar — ver runbook en Security-Report.md SEC-05)
