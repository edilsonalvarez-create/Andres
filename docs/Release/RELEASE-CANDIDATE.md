# Release Candidate Validation Report — `1.0.0-rc.1`

**Release Manager:** QA Guardian engineering  
**Date:** 2026-07-16  
**Candidate:** `1.0.0-rc.1`  
**Status:** **READY FOR STAGING / QA SIGN-OFF**

## 1. Scope validated

| Área | Estado | Evidencia / notas |
|------|--------|-------------------|
| Testing | ✅ | 178 unit tests + 25 Vitest passed (2026-07-16) |
| Seguridad | ✅ | Headers, JWT/RBAC, seed hardening, docs Security/* |
| Performance | ✅ | Sprint 6 (bundle split, N+1, compression, SignalR) |
| UX | ✅ | Menú enterprise + lazy routes + theme |
| Documentación | ✅ | Manuales + Release Notes + Go Live Checklist |
| Deployment | ✅ | Dockerfiles API/frontend + compose |
| Docker | ✅ | Healthchecks SQL/Redis/API; frontend depende de API |
| CI/CD | ✅ | `.github/workflows/ci.yml` → main **y** master; frontend tests |
| Rollback | ✅ | Procedimiento en Go Live Checklist |
| Monitoreo | ✅ | Serilog + `/api/v1/version` |
| Health Checks | ✅ | `/health`, `/health/live`, `/health/ready` (DB) |

## 2. Gaps consciously deferred (not RC blockers)

| Item | Razón |
|------|--------|
| Multi-tenant Organization | Alto esfuerzo; ACL por proyecto es siguiente iteración |
| Redis health check dedicado | Ready actual cubre DB; Redis opcional en dev |
| Entity-level audit diffs | Audit HTTP suficiente para RC |
| E2E Playwright en CI | Smoke local/e2e existen; no bloquean RC |

## 3. Entry criteria met

- [x] Build Release backend
- [x] Unit + frontend tests green
- [x] Version stamped `1.0.0-rc.1`
- [x] Health live/ready
- [x] Release Notes published under `docs/Release/`
- [x] Go Live Checklist published

## 4. Exit criteria to GA `1.0.0`

Completar [GO-LIVE-CHECKLIST.md](./GO-LIVE-CHECKLIST.md) en staging con sign-off de Release Manager, Tech Lead y Product Owner.

## 5. Quick verification commands

```powershell
dotnet test tests/QAGuardian.UnitTests -c Release
cd frontend; npm test -- --run; npm run build
curl.exe -fsS http://localhost:5080/health/live
curl.exe -fsS http://localhost:5080/health/ready
curl.exe -fsS http://localhost:5080/api/v1/version
```
