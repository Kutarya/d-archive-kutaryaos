param([switch]$PaketlemeYok)
$ErrorActionPreference = 'Stop'
$kutaryaRoot = Split-Path -Parent $PSScriptRoot
$kutaryaSdk = Join-Path $kutaryaRoot 'Bagimliliklar\dotnet\dotnet.exe'
$env:DOTNET_ROOT = Split-Path -Parent $kutaryaSdk
$env:DOTNET_CLI_HOME = Join-Path $kutaryaRoot 'Bagimliliklar\cli'
$env:NUGET_PACKAGES = Join-Path $kutaryaRoot 'Bagimliliklar\nuget'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
if (-not (Test-Path -LiteralPath $kutaryaSdk)) { throw '.NET SDK bulunamadı.' }
Push-Location $PSScriptRoot
try {
    & $kutaryaSdk build KutaryaOS.sln -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Derleme başarısız.' }
    $kutaryaApp = Join-Path $PSScriptRoot 'src\KutaryaOS.WinForms\bin\Release\net10.0-windows\KutaryaOS.dll'
    & $kutaryaSdk $kutaryaApp --self-test --data (Join-Path $PSScriptRoot 'TestSonuclari\Core')
    if ($LASTEXITCODE -ne 0) { throw 'Çekirdek kabulü başarısız.' }
    & $kutaryaSdk $kutaryaApp --transfer-tests --data (Join-Path $PSScriptRoot 'TestSonuclari\Aktarim')
    if ($LASTEXITCODE -ne 0) { throw 'AI sınırı / ana PC aktarımı kabulü başarısız.' }
    & $kutaryaSdk $kutaryaApp --model-tests --data (Join-Path $PSScriptRoot 'TestSonuclari\Model')
    if ($LASTEXITCODE -ne 0) { throw 'Model indirme kabulü başarısız.' }
    if (Test-Path -LiteralPath (Join-Path $kutaryaRoot 'Veri\YerelAI\Kutarya-Us-Hafif.gguf')) {
        & $kutaryaSdk $kutaryaApp --ui-local-smoke --data (Join-Path $PSScriptRoot 'TestSonuclari\UI') --model-data (Join-Path $kutaryaRoot 'Veri')
    } else {
        & $kutaryaSdk $kutaryaApp --ui-smoke --data (Join-Path $PSScriptRoot 'TestSonuclari\UI')
    }
    if ($LASTEXITCODE -ne 0) { throw 'UI kabulü başarısız.' }
    if (-not $PaketlemeYok) {
        $kutaryaOutput = Join-Path $kutaryaRoot 'Dagitim\KutaryaOS-0.3.0'
        & $kutaryaSdk publish src\KutaryaOS.WinForms\KutaryaOS.WinForms.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=false -p:PublishTrimmed=false -o $kutaryaOutput
        if ($LASTEXITCODE -ne 0) { throw 'Bağımsız EXE üretilemedi. Açık aynı sürümü kapat.' }
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Destination (Join-Path $kutaryaOutput 'OKU.md') -Force
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'docs') -Destination $kutaryaOutput -Recurse -Force
        $kutaryaCompiler = Join-Path $kutaryaRoot 'Bagimliliklar\InnoSetup\ISCC.exe'
        if (-not (Test-Path -LiteralPath $kutaryaCompiler)) { throw 'Inno Setup derleyicisi bulunamadı.' }
        & $kutaryaCompiler (Join-Path $PSScriptRoot 'installer\KutaryaOS.iss')
        if ($LASTEXITCODE -ne 0) { throw 'Setup üretilemedi.' }
    }
    Write-Output 'Build, çekirdek, indirme ve WinForms kontrolleri geçti.'
}
finally { Pop-Location }

