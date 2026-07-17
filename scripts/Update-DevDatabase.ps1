<#
.SYNOPSIS
  Helper de desarrollo para el SQLite local (qaguardian-dev.db).

.DESCRIPTION
  En Development, con Database:ApplyMigrationsOnStartup=true:
  - SQL Server → MigrateAsync (migraciones EF).
  - SQLite → EnsureCreated desde el modelo actual (sin parches ad-hoc).

  Las migraciones versionadas en Migrations/ son la fuente de verdad de producción
  (SQL Server) y se aplican con .\scripts\Migrate-Database.ps1 o compose `migrate`.

  Si el SQLite quedó desfasado (p. ej. columnas nuevas), use -Reset y vuelva a
  arrancar la API. No toca secretos ni .env.

.PARAMETER Reset
  Elimina src/QAGuardian.API/qaguardian-dev.db (+ -shm/-wal).

.EXAMPLE
  .\scripts\Update-DevDatabase.ps1 -Reset
  dotnet run --project src/QAGuardian.API
#>
[CmdletBinding()]
param(
    [switch] $Reset
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$apiDir = Join-Path $repoRoot "src/QAGuardian.API"
$dbPath = Join-Path $apiDir "qaguardian-dev.db"

if ($Reset) {
    foreach ($f in @($dbPath, "$dbPath-shm", "$dbPath-wal")) {
        if (Test-Path $f) {
            Remove-Item -Force $f
            Write-Host "Eliminado: $f"
        }
    }
}
else {
    Write-Host "SQLite: $dbPath (existe=$(Test-Path $dbPath))"
    Write-Host "Si el esquema queda desfasado: .\scripts\Update-DevDatabase.ps1 -Reset"
}

Write-Host "Siguiente paso (Development):"
Write-Host "  dotnet run --project src/QAGuardian.API"
Write-Host "Producción / SQL Server: .\scripts\Migrate-Database.ps1 -ConnectionString '...'"
