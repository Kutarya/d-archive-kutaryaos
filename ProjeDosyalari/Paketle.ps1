$ErrorActionPreference = 'Stop'
$kutaryaRoot = Split-Path -Parent $PSScriptRoot
$kutaryaDist = Join-Path $kutaryaRoot 'Dagitim'
$kutaryaPayload = Join-Path $kutaryaDist 'KutaryaOS-0.3.0'
$kutaryaSetup = Join-Path $kutaryaDist 'KutaryaOS-Setup-0.3.0-x64.exe'
$kutaryaReceipt = Get-Content (Join-Path $PSScriptRoot 'TestSonuclari\Kurulum\kurulum-kabul.json') -Raw | ConvertFrom-Json
$kutaryaExe = Join-Path $kutaryaPayload 'KutaryaOS.exe'
if (-not $kutaryaReceipt.passed -or $kutaryaReceipt.installerSha -ne (Get-FileHash $kutaryaSetup -Algorithm SHA256).Hash -or $kutaryaReceipt.exeSha -ne (Get-FileHash $kutaryaExe -Algorithm SHA256).Hash) {
    throw 'Paketlenecek son setup/EXE kabul raporuyla eşleşmiyor.'
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$kutaryaPortable = Join-Path $kutaryaDist 'KutaryaOS-Windows-x64-0.3.0.zip'
$kutaryaSource = Join-Path $kutaryaDist 'KutaryaOS-Kaynak-0.3.0.zip'
foreach ($kutaryaZip in @($kutaryaPortable, $kutaryaSource)) {
    if (-not [IO.Path]::GetFullPath($kutaryaZip).StartsWith([IO.Path]::GetFullPath($kutaryaDist) + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Arşiv hedefi dağıtım klasörü dışında.' }
    if (Test-Path -LiteralPath $kutaryaZip) { Remove-Item -LiteralPath $kutaryaZip -Force }
}
[IO.Compression.ZipFile]::CreateFromDirectory($kutaryaPayload, $kutaryaPortable, [IO.Compression.CompressionLevel]::Optimal, $false)
$kutaryaArchive = [IO.Compression.ZipFile]::Open($kutaryaSource, [IO.Compression.ZipArchiveMode]::Create)
try {
    $kutaryaFiles = Get-ChildItem -LiteralPath $PSScriptRoot -Recurse -File -Force
    foreach ($kutaryaFile in $kutaryaFiles) {
        $kutaryaRelative = [IO.Path]::GetRelativePath($PSScriptRoot, $kutaryaFile.FullName)
        if (($kutaryaFile.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { continue }
        if ($kutaryaRelative -match '(^|\\)(bin|obj|\.git|\.vs|TestSonuclari|Veri|SanalCihazlar|YerelAI|UIKosular|Kosular|ModelKosular)(\\|$)') { continue }
        if ($kutaryaRelative -match '\.(dpapi|gguf|part|log|user|suo)$' -or $kutaryaFile.Name -in @('cihaz.json','tema.txt','son-hata.txt','kabul.json','model-kabul.json','ui-kabul.json')) { continue }
        if ($kutaryaRelative.StartsWith('..')) { throw 'Kaynak kökü dışında dosya.' }
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($kutaryaArchive, $kutaryaFile.FullName, $kutaryaRelative.Replace('\','/'), [IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally { $kutaryaArchive.Dispose() }
$kutaryaReadZip = [IO.Compression.ZipFile]::OpenRead($kutaryaSource)
try {
    $kutaryaPrivate = @($kutaryaReadZip.Entries | Where-Object { $_.FullName -match '(^|/)(bin|obj|TestSonuclari|Veri|SanalCihazlar|YerelAI)/|\.(dpapi|gguf|part|log)$' })
    if ($kutaryaPrivate.Count -gt 0) { throw 'Kaynak arşivinde dışlanması gereken veri var.' }
    $kutaryaEntryCount = $kutaryaReadZip.Entries.Count
} finally { $kutaryaReadZip.Dispose() }
$kutaryaShots = Join-Path $kutaryaDist 'Goruntuler'
New-Item -ItemType Directory -Path $kutaryaShots -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'TestSonuclari\Kurulum\UI') -Filter '*.png' -File | Copy-Item -Destination $kutaryaShots -Force
$kutaryaGallery = @"
# Kutarya OS 0.3 — uygulama görüntüleri

Kurulan gerçek WinForms uygulamasının kendi kontrol ağacından alınan görüntüler.
Show / PerformClick / DrawToBitmap; fiziksel masaüstü ekran yakalama değildir.
Yapay zekâ yalnız arka plan görselini üretmiştir; uygulama görüntüleri maket değildir.

## İlk açılış

![Cihaz kurulumu]($($kutaryaShots.Replace('\','/'))/ilk-kurulum.png)

## Masaüstü

![Kutarya OS masaüstü]($($kutaryaShots.Replace('\','/'))/masaustu.png)

## Gerçek yerel AI ile dosya işlemi

![AI paneli ve gerçek klasör oluşturma]($($kutaryaShots.Replace('\','/'))/yerel-ai-sohbet.png)

## İki AI seçeneği

![Yerel model ve kendi API ayarları]($($kutaryaShots.Replace('\','/'))/ai-secimleri.png)

## Başlangıç menüsü

![Uygulama menüsü]($($kutaryaShots.Replace('\','/'))/baslangic-menusu.png)

## Kullanıcının başlattığı ana PC aktarımı

![Gerçek klasör aktarımı]($($kutaryaShots.Replace('\','/'))/ana-pc-aktarim.png)

Hedef seçimi testte geçici klasörle verilmiştir; taşıma düğmesi gerçek dosyaları
taşımıştır. Windows klasör seçicisi fiziksel fareyle bu koşuda denenmemiştir.
"@
Set-Content -LiteralPath (Join-Path $kutaryaDist 'UYGULAMA-GORUNTULERI.md') -Value $kutaryaGallery -Encoding utf8
$kutaryaArtifacts = @($kutaryaSetup,$kutaryaExe,$kutaryaPortable,$kutaryaSource) | ForEach-Object {
    [ordered]@{ name=[IO.Path]::GetFileName($_); bytes=(Get-Item -LiteralPath $_).Length; sha256=(Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash }
}
$kutaryaManifest = [ordered]@{
    version='0.3.0'; date=(Get-Date).ToString('o'); artifacts=$kutaryaArtifacts;
    tests=[ordered]@{core=34;desktopBoundaryAndTransfer=34;modelDownload=8;uiWithRealNativeModel=20;setup=10};
    sourceEntries=$kutaryaEntryCount; privateEntries=0;
    model=[ordered]@{bundled=$false;bytes=639446688;sha256='9465e63a22add5354d9bb4b99e90117043c7124007664907259bd16d043bb031'};
    realAiServerRequired=$false
}
$kutaryaManifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $kutaryaDist 'paket-bilgisi-0.3.0.json') -Encoding utf8
Write-Output "Paketler tamamlandı. Kaynak: $kutaryaEntryCount dosya, 0 özel veri."


