<#
.SYNOPSIS
  Aplica migraciones EF Core a SQL Server (job aparte del proceso API).

.DESCRIPTION
  Sprint 16-A: en Production/QA/Staging el API NO ejecuta Migrate/EnsureCreated.
  Use este script (o el servicio compose `migrate`) antes de levantar la API.

.PARAMETER ConnectionString
  Connection string SQL Server. Si se omite, usa $env:ConnectionStrings__DefaultConnection
  o $env:QA_GUARDIAN_CONNECTION.

.EXAMPLE
  .\scripts\Migrate-Database.ps1 -ConnectionString "Server=localhost,1433;Database=QAGuardian;User Id=sa;Password=...;TrustServerCertificate=True"
#>
[CmdletBinding()]
param(
    [string] $ConnectionString = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $repoRoot

if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    $ConnectionString = $env:ConnectionStrings__DefaultConnection
    if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
        $ConnectionString = $env:QA_GUARDIAN_CONNECTION
    }
}

if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    throw "Defina -ConnectionString o la variable ConnectionStrings__DefaultConnection / QA_GUARDIAN_CONNECTION."
}

$env:ConnectionStrings__DefaultConnection = $ConnectionString

Write-Host "Restaurando herramienta local dotnet-ef..."
dotnet tool restore | Out-Host

Write-Host "Aplicando migraciones EF (database update)..."
dotnet ef database update `
    --project src/QAGuardian.Infrastructure/QAGuardian.Infrastructure.csproj `
    --startup-project src/QAGuardian.API/QAGuardian.API.csproj `
    --connection $ConnectionString

Write-Host "Migraciones aplicadas correctamente."
