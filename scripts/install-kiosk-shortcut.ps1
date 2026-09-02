# scripts/install-kiosk-shortcut.ps1
#
# Crea un acceso directo (.lnk) en la carpeta de Inicio del usuario actual
# que abre Chrome en modo --kiosk apuntando a http://localhost:5000. Asi,
# al iniciar sesion Windows, Chrome arranca en pantalla completa contra
# el servicio SuperMWeb.
#
# Uso:
#   ./scripts/install-kiosk-shortcut.ps1
#   ./scripts/install-kiosk-shortcut.ps1 -Url "http://localhost:5000/venta"

[CmdletBinding()]
param(
    [string]$Url = "http://localhost:5000",
    [string]$ChromePath = ""
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Autodescubrir Chrome si no vino por parametro.
if ([string]::IsNullOrWhiteSpace($ChromePath)) {
    $candidates = @(
        "$env:ProgramFiles\Google\Chrome\Application\chrome.exe",
        "${env:ProgramFiles(x86)}\Google\Chrome\Application\chrome.exe",
        "$env:LocalAppData\Google\Chrome\Application\chrome.exe"
    )
    $ChromePath = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $ChromePath) {
        throw "No se encontro chrome.exe. Instala Google Chrome o pasa -ChromePath."
    }
}

$startup = [Environment]::GetFolderPath('Startup')
if (-not (Test-Path $startup)) {
    throw "No se encontro la carpeta de inicio del usuario: $startup"
}

$lnk = Join-Path $startup 'SuperM Kiosco.lnk'

# Argumentos de Chrome:
# --kiosk           pantalla completa sin chrome de UI
# --new-window      fuerza ventana nueva
# --no-first-run    evita pantallas de bienvenida
# --disable-features=Translate  evita popup de traduccion
# --incognito       ya no hace falta porque son maquinas dedicadas
$chromeArgs = "--kiosk --new-window --no-first-run --disable-features=Translate `"$Url`""

Write-Host "==> Creando acceso directo" -ForegroundColor Cyan
Write-Host "    Origen:   $ChromePath"
Write-Host "    Args:     $chromeArgs"
Write-Host "    Destino:  $lnk"

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($lnk)
$shortcut.TargetPath = $ChromePath
$shortcut.Arguments = $chromeArgs
$shortcut.WindowStyle = 3 # Maximized
$shortcut.Description = "SuperM POS en modo kiosco"
$shortcut.Save()

Write-Host "==> Acceso directo creado" -ForegroundColor Green
Write-Host ""
Write-Host "El proximo inicio de sesion abrira Chrome en modo kiosco."
Write-Host "Para salir del modo kiosco: Ctrl+F4 o Alt+F4."
Write-Host ""
Write-Host "Para probarlo ahora mismo, abrir el acceso directo:"
Write-Host "    Invoke-Item '$lnk'"
