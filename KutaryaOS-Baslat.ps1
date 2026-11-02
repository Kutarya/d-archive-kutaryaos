$ErrorActionPreference = 'Stop'
$kutaryaExe = Join-Path $PSScriptRoot 'Dagitim\KutaryaOS-0.3.0\KutaryaOS.exe'
$kutaryaData = Join-Path $PSScriptRoot 'Veri'
if (-not (Test-Path -LiteralPath $kutaryaExe)) { throw 'KutaryaOS.exe bulunamadı. Önce ProjeDosyalari\Derle.ps1 çalıştır.' }
# Kullanıcının çalışacağı ana uygulama görünür açılır; arka plan yardımcısı değildir.
Start-Process -FilePath $kutaryaExe -ArgumentList @('--data', $kutaryaData) -WorkingDirectory (Split-Path -Parent $kutaryaExe)

