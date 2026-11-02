param([switch]$Hizli)
$ErrorActionPreference = 'Stop'
$kutaryaRoot = Split-Path -Parent $PSScriptRoot
$kutaryaInstaller = Join-Path $kutaryaRoot 'Dagitim\KutaryaOS-Setup-0.3.0-x64.exe'
$kutaryaInstallDir = 'D:\KutaryaOS\TestKurulum03'
$kutaryaReport = Join-Path $PSScriptRoot 'TestSonuclari\Kurulum'
$kutaryaRegistry = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{13D41EA6-1CC1-4E75-B48C-8229527227B2}_is1'
$kutaryaChecks = [System.Collections.Generic.List[string]]::new()
function Check-Kutarya([bool]$condition, [string]$message) {
    if (-not $condition) { throw $message }
    $kutaryaChecks.Add($message)
}
function Run-Kutarya([string]$exe, [string[]]$arguments) {
    $kutaryaProcess = Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
    if ($kutaryaProcess.ExitCode -ne 0) { throw "Çalıştırıcı hata kodu: $($kutaryaProcess.ExitCode) / $exe" }
}
$kutaryaResolved = [IO.Path]::GetFullPath($kutaryaInstallDir)
if ($kutaryaResolved -ne 'D:\KutaryaOS\TestKurulum03') { throw 'Test hedefi beklenen klasör değil.' }
if (Test-Path -LiteralPath $kutaryaRegistry) {
    $kutaryaPrior = Get-ItemProperty -LiteralPath $kutaryaRegistry
    if ($kutaryaPrior.InstallLocation.TrimEnd('\') -ne $kutaryaResolved) { throw 'Başka kullanıcı kurulumu var; bu kabul testi onu değiştirmez.' }
}
New-Item -ItemType Directory -Path $kutaryaReport -Force | Out-Null
try {
    Run-Kutarya $kutaryaInstaller @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/NOICONS',"/DIR=$kutaryaInstallDir","/LOG=$kutaryaReport\install.log")
    $kutaryaApp = Join-Path $kutaryaInstallDir 'KutaryaOS.exe'
    $kutaryaExpected = (Get-FileHash -LiteralPath (Join-Path $kutaryaRoot 'Dagitim\KutaryaOS-0.3.0\KutaryaOS.exe') -Algorithm SHA256).Hash
    Check-Kutarya ((Get-FileHash -LiteralPath $kutaryaApp -Algorithm SHA256).Hash -eq $kutaryaExpected) 'Kurulan EXE dağıtım dosyasıyla aynı'
    Check-Kutarya (Test-Path -LiteralPath $kutaryaRegistry) 'Windows uygulama kaldırma kaydı oluşturuldu'
    $kutaryaSentinel = Join-Path $kutaryaInstallDir 'SanalCihazlar\kurulum-test-notu.txt'
    New-Item -ItemType Directory -Path (Split-Path -Parent $kutaryaSentinel) -Force | Out-Null
    Set-Content -LiteralPath $kutaryaSentinel -Value 'Yalnız test verisidir, korunmalı.' -Encoding utf8
    if (-not $Hizli) {
        Remove-Item Env:DOTNET_ROOT -ErrorAction SilentlyContinue
        Run-Kutarya $kutaryaApp @('--self-test','--data',"$kutaryaReport\Core")
        $kutaryaCore = Get-Content -LiteralPath "$kutaryaReport\Core\kabul.json" -Raw | ConvertFrom-Json
        Check-Kutarya ($kutaryaCore.passed -eq 34 -and $kutaryaCore.failures -eq 0) 'Kurulu uygulama 34 çekirdek kontrolünü geçti'
        Run-Kutarya $kutaryaApp @('--transfer-tests','--data',"$kutaryaReport\Aktarim")
        $kutaryaTransfer = Get-Content -LiteralPath "$kutaryaReport\Aktarim\aktarim-kabul.json" -Raw | ConvertFrom-Json
        Check-Kutarya ($kutaryaTransfer.passed -and $kutaryaTransfer.count -eq 34) 'Kurulu uygulama 34 AI sınırı ve aktarım kontrolünü geçti'
        Run-Kutarya $kutaryaApp @('--model-tests','--data',"$kutaryaReport\Model")
        $kutaryaModel = Get-Content -LiteralPath "$kutaryaReport\Model\model-kabul.json" -Raw | ConvertFrom-Json
        Check-Kutarya ($kutaryaModel.passed -and $kutaryaModel.count -eq 8) 'Kurulu uygulama 8 indirme kontrolünü geçti'
        Run-Kutarya $kutaryaApp @('--ui-local-smoke','--data',"$kutaryaReport\UI",'--model-data',"$kutaryaRoot\Veri")
        $kutaryaUi = Get-Content -LiteralPath "$kutaryaReport\UI\ui-kabul.json" -Raw | ConvertFrom-Json
        Check-Kutarya ($kutaryaUi.passed -and $kutaryaUi.count -eq 20) 'Kurulu uygulama 20 gerçek yerel AI ve UI kontrolünü geçti'
    }
    Run-Kutarya $kutaryaInstaller @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/NOICONS',"/DIR=$kutaryaInstallDir","/LOG=$kutaryaReport\reinstall.log")
    Check-Kutarya ((Get-Content -LiteralPath $kutaryaSentinel -Raw).Contains('korunmalı')) 'Yeniden kurulum kullanıcı dosyasını korudu'
    Run-Kutarya (Join-Path $kutaryaInstallDir 'unins000.exe') @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',"/LOG=$kutaryaReport\uninstall.log")
    Check-Kutarya (-not (Test-Path -LiteralPath $kutaryaApp)) 'Kaldırma kurulu programı kaldırdı'
    Check-Kutarya (Test-Path -LiteralPath $kutaryaSentinel) 'Kaldırma kullanıcı dosyasını korudu'
    Check-Kutarya (-not (Test-Path -LiteralPath $kutaryaRegistry)) 'Kaldırma Windows uygulama kaydını kaldırdı'
    $kutaryaReceipt = [ordered]@{ passed=$true; count=$kutaryaChecks.Count; checks=$kutaryaChecks; fast=[bool]$Hizli; installerSha=(Get-FileHash -LiteralPath $kutaryaInstaller -Algorithm SHA256).Hash; exeSha=$kutaryaExpected; target=$kutaryaInstallDir; date=(Get-Date).ToString('o') }
    $kutaryaName = if ($Hizli) { 'kurulum-final.json' } else { 'kurulum-kabul.json' }
    $kutaryaReceipt | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $kutaryaReport $kutaryaName) -Encoding utf8
    Write-Output "Kurulum kontrolü geçti: $($kutaryaChecks.Count)."
} catch {
    [ordered]@{passed=$false;checks=$kutaryaChecks;error=$_.ToString()} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $kutaryaReport 'kurulum-hata.json') -Encoding utf8
    throw
}


