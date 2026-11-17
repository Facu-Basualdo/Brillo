# Baja makeappx.exe y makepri.exe (para armar el paquete de la Microsoft Store) sin instalar el Windows SDK entero:
# usa el paquete oficial de Microsoft "Microsoft.Windows.SDK.BuildTools" de NuGet (unos 20 MB) y lo descomprime en
# herramientas\sdk (que no se sube al repo).
# Uso: powershell -ExecutionPolicy Bypass -File herramientas\bajar-sdk.ps1

$ErrorActionPreference = 'Stop'
$destino = Join-Path $PSScriptRoot "sdk"
$paquete = "microsoft.windows.sdk.buildtools"

$version = (Invoke-RestMethod "https://api.nuget.org/v3-flatcontainer/$paquete/index.json").versions |
    Where-Object { $_ -notmatch '-' } | Select-Object -Last 1
"Bajando $paquete $version..."

New-Item -ItemType Directory -Force $destino | Out-Null
$zip = Join-Path $destino "buildtools.zip"
Invoke-WebRequest "https://api.nuget.org/v3-flatcontainer/$paquete/$version/$paquete.$version.nupkg" -OutFile $zip -UseBasicParsing
if (Test-Path "$destino\paquete") { Remove-Item -Recurse -Force "$destino\paquete" }
Expand-Archive $zip "$destino\paquete"
Remove-Item $zip

# Verificamos que las herramientas estén firmadas por Microsoft antes de usarlas.
Get-ChildItem "$destino\paquete\bin" -Recurse -Include makeappx.exe, makepri.exe | Where-Object { $_.DirectoryName -like '*\x64' } | ForEach-Object {
    $firma = Get-AuthenticodeSignature $_.FullName
    if ($firma.Status -ne 'Valid' -or $firma.SignerCertificate.Subject -notlike '*Microsoft*') { throw "Firma inválida en $($_.FullName)" }
    "OK: $($_.FullName)"
}
