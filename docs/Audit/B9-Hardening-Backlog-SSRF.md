# B9 / Hardening — deuda técnica SSRF (residuales B3)

**Origen:** revisión de B3 (PR #6) — **APPROVE** con residuales explícitos.  
**Política:** **no** entran en B4 ni en B5–B8 del stack actual. Asignados a **B9 (Hardening)** / backlog técnico.  
**Fecha registro:** 2026-07-21.

## Ítems

| ID | Hallazgo | Por qué no va en B4 | Destino |
|----|----------|---------------------|---------|
| B9-SSRF-01 | **Tests de pinning IPv6** — cobertura de `ConnectCallback` / `ResolvePinnedAddress` con destinos IPv6 (ULA, link-local, mapped) insuficiente frente a IPv4. | Amplía superficie de tests SSRF, fuera de EncryptionKey. | B9 Hardening |
| B9-SSRF-02 | **Redirect 307/308 con `HttpContent` reutilizable** — reenvío de body en hops 307/308 puede fallar si el content no es reutilizable; endurecer o documentar contrato. | Cambio de handler HTTP/SSRF. | B9 Hardening |
| B9-SSRF-03 | **Integración Redirect + `ConnectCallback`** — tests actuales de redirects no ejercitan el pin de IP en el mismo flujo end-to-end. | Test de integración SSRF, no fail-fast de clave. | B9 Hardening |
| B9-SSRF-04 | **Factory SSRF para `IGitHubClient`** — cliente GitHub (host fijo `api.github.com`) fuera de `SsrfHttpHandlerFactory`; residual documentado en checklist. | Cableado HTTP/DI SSRF. | B9 Hardening |

## Fuera de este registro

- B5 Hangfire schema, B6 ACL, B7 masking UI, B8 sandbox limits: **no iniciados** hasta autorización explícita tras merge/aprobación de B4.
- Formato preexistente fuera de B4 (`ApprovalCommands.cs`, `TestRunners.cs` en `dotnet format --verify-no-changes` de solución completa): deuda de estilo aparte; no bloquea el diff B4 (verify scoped a archivos B4 = OK).

## Criterio de cierre (B9)

Cada ítem requiere test o cableado verde + actualización de `docs/Security/Security-Checklist.md` (residual B3).
