# Lanzador de QA Guardian: arranca API (.NET) + Frontend (Vite) y abre el navegador.
# No duplica procesos si los puertos ya estan en uso.
$ErrorActionPreference = "SilentlyContinue"
$root = "C:\Users\edilson.alvarez\Documents\Grabaciones de sonido\QAGuardian"

function Test-Port([int]$p) {
  [bool](Get-NetTCPConnection -State Listen -LocalPort $p -ErrorAction SilentlyContinue)
}

# --- API (.NET) en el puerto 5080 ---
# Importante: --no-launch-profile NO carga launchSettings.json.
# Se exportan las mismas variables de Development para Seed/JWT/cifrado.
if (-not (Test-Port 5080)) {
  $env:ASPNETCORE_ENVIRONMENT = "Development"
  $env:ASPNETCORE_URLS = "http://localhost:5080"
  if (-not $env:Jwt__SigningKey) { $env:Jwt__SigningKey = "dev-only-signing-key-min-32-chars!!" }
  if (-not $env:Security__EncryptionKey) { $env:Security__EncryptionKey = "dev-only-encryption-key-32b!!" }
  if (-not $env:Seed__AdminEmail) { $env:Seed__AdminEmail = "admin@qaguardian.local" }
  if (-not $env:Seed__AdminPassword) { $env:Seed__AdminPassword = "QaGuardian.2026!" }
  Start-Process -FilePath "dotnet" `
    -ArgumentList "run", "--project", "src/QAGuardian.API", "--no-launch-profile" `
    -WorkingDirectory $root -WindowStyle Minimized
}

# --- Frontend (Vite) en el puerto 5173 ---
if (-not (Test-Port 5173)) {
  $npm = (Get-Command npm.cmd -ErrorAction SilentlyContinue).Source
  if (-not $npm) { $npm = "npm.cmd" }
  Start-Process -FilePath $npm -ArgumentList "run", "dev" `
    -WorkingDirectory (Join-Path $root "frontend") -WindowStyle Minimized
}

# --- Esperar a que ambos respondan (hasta ~90s) y abrir el navegador ---
for ($i = 0; $i -lt 45; $i++) {
  if ((Test-Port 5173) -and (Test-Port 5080)) { break }
  Start-Sleep -Seconds 2
}
Start-Sleep -Seconds 2
Start-Process "http://localhost:5173/"
