<#
  NooXAIR tam derleme hatti
    1) Ikon / kurulum gorselleri
    2) NooXAIR.exe (C#, .NET Framework 4.8 csc - ek SDK gerekmez)
    3) Native UxPlay motoru + GStreamer (MSYS2 UCRT64; eksikse MSYS2 otomatik indirilir)
    4) Bonjour64.msi (Apple'dan indirilip pakete gomulur - cevrimdisi kurulum icin)
    5) Inno Setup (eksikse otomatik indirilir) -> dist\NooXAIR_Setup.exe
#>
param(
    [switch]$RebuildEngine,
    [switch]$SkipEngine,
    [string]$UxPlayRef = "master",
    [string]$MsysRoot = "C:\msys64"
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch {}

$Root    = Split-Path -Parent $PSScriptRoot
$Build   = Join-Path $Root "build"
$Payload = Join-Path $Build "payload"
$Cache   = Join-Path $Build "cache"
$Dist    = Join-Path $Root "dist"
foreach ($d in @($Build, $Payload, $Cache, $Dist)) { if (-not (Test-Path $d)) { New-Item -ItemType Directory -Path $d -Force | Out-Null } }

function Step($n, $t) { Write-Host ""; Write-Host "[$n] $t" -ForegroundColor Cyan }
function Ok($t)   { Write-Host "    [OK] $t" -ForegroundColor Green }
function Warn($t) { Write-Host "    [!]  $t" -ForegroundColor Yellow }
function Fail($t) { Write-Host "    [HATA] $t" -ForegroundColor Red; exit 1 }

function Get-File($url, $dest) {
    Write-Host "    indiriliyor: $url"
    for ($i = 1; $i -le 3; $i++) {
        try { Invoke-WebRequest -Uri $url -OutFile $dest -UseBasicParsing -TimeoutSec 600; if ((Get-Item $dest).Length -gt 0) { return $true } }
        catch { Warn "deneme $i basarisiz: $($_.Exception.Message)"; Start-Sleep -Seconds 2 }
    }
    return $false
}

function To-Unix([string]$p) {
    $p = $p -replace '\\', '/'
    if ($p -match '^([A-Za-z]):(.*)$') { return "/" + $Matches[1].ToLower() + $Matches[2] }
    return $p
}

function Invoke-Msys([string]$cmd) {
    $env:MSYSTEM = "UCRT64"; $env:CHERE_INVOKING = "1"; $env:MSYS2_PATH_TYPE = "minimal"
    & "$MsysRoot\usr\bin\bash.exe" -lc $cmd
    return $LASTEXITCODE
}

Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host "   NooXAIR v3 - Derleme ve Kurulum Paketi Olusturucu" -ForegroundColor Cyan
Write-Host "=====================================================" -ForegroundColor Cyan

# ------------------------------------------------------------------ 1) ikon
Step "1/5" "Ikon ve kurulum gorselleri"
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "make_icon.ps1")
if (-not (Test-Path "$Root\assets\nooxair.ico")) { Fail "ikon uretilemedi" }

# ------------------------------------------------------------------ 2) uygulama
Step "2/5" "NooXAIR.exe derleniyor (C# / .NET Framework 4.8)"
$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { $csc = "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe" }
if (-not (Test-Path $csc)) { Fail ".NET Framework 4.x derleyicisi (csc.exe) bulunamadi" }
$srcFiles = Get-ChildItem "$Root\src\*.cs" | ForEach-Object { $_.FullName }
& $csc /nologo /codepage:65001 /target:winexe /optimize+ /platform:anycpu /nowarn:414 `
    "/out:$Payload\NooXAIR.exe" "/win32icon:$Root\assets\nooxair.ico" `
    /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.ServiceProcess.dll `
    $srcFiles
if ($LASTEXITCODE -ne 0) { Fail "C# derlemesi basarisiz" }
Ok "NooXAIR.exe"

# ------------------------------------------------------------------ 3) motor
Step "3/5" "Native UxPlay motoru (MSYS2 / GStreamer)"
$engineOut = Join-Path $Payload "engine"
$engineExe = Join-Path $engineOut "bin\uxplay.exe"
if ($SkipEngine) {
    Warn "motor derlemesi atlandi (-SkipEngine)"
} elseif ((Test-Path $engineExe) -and -not $RebuildEngine) {
    Ok "mevcut motor kullaniliyor (yeniden derlemek icin: build.bat rebuild)"
} else {
    if (-not (Test-Path "$MsysRoot\usr\bin\bash.exe")) {
        Write-Host "    MSYS2 bulunamadi, indiriliyor (~100 MB)..."
        $sfx = Join-Path $Cache "msys2-base.sfx.exe"
        $ok = Get-File "https://github.com/msys2/msys2-installer/releases/download/nightly-x86_64/msys2-base-x86_64-latest.sfx.exe" $sfx
        if (-not $ok) { $ok = Get-File "https://repo.msys2.org/distrib/msys2-x86_64-latest.sfx.exe" $sfx }
        if (-not $ok) { Fail "MSYS2 indirilemedi" }
        $parent = Split-Path -Parent $MsysRoot
        & $sfx -y "-o$parent\" | Out-Null
        if (-not (Test-Path "$MsysRoot\usr\bin\bash.exe")) { Fail "MSYS2 acilamadi: $MsysRoot" }
        Invoke-Msys "true" | Out-Null   # ilk calistirma: anahtarlar / profil
        Ok "MSYS2 kuruldu: $MsysRoot"
    }

    Write-Host "    MSYS2 paketleri guncelleniyor (ilk seferde uzun surebilir)..."
    Invoke-Msys "pacman -Syu --noconfirm" | Out-Null      # cekirdek guncellemesi (msys2-runtime) kabugu kapatabilir
    Invoke-Msys "pacman -Syu --noconfirm" | Out-Null
    $pkgs = "mingw-w64-ucrt-x86_64-gcc mingw-w64-ucrt-x86_64-binutils mingw-w64-ucrt-x86_64-cmake mingw-w64-ucrt-x86_64-ninja " +
            "mingw-w64-ucrt-x86_64-pkgconf mingw-w64-ucrt-x86_64-openssl mingw-w64-ucrt-x86_64-libplist " +
            "mingw-w64-ucrt-x86_64-gstreamer mingw-w64-ucrt-x86_64-gst-plugins-base mingw-w64-ucrt-x86_64-gst-plugins-good " +
            "mingw-w64-ucrt-x86_64-gst-plugins-bad mingw-w64-ucrt-x86_64-gst-libav git p7zip"
    if ((Invoke-Msys "pacman -S --noconfirm --needed $pkgs") -ne 0) { Fail "MSYS2 paketleri kurulamadi" }

    $script = To-Unix (Join-Path $PSScriptRoot "build_engine.sh")
    if ((Invoke-Msys "bash '$script' '$(To-Unix $engineOut)' '$UxPlayRef'") -ne 0) { Fail "UxPlay motoru derlenemedi" }
    if (-not (Test-Path $engineExe)) { Fail "uxplay.exe olusmadi" }
    Ok "motor hazir: $engineOut"
}

# Motor duman testi: GStreamer eklentileri yuklenebiliyor mu?
if (Test-Path $engineExe) {
    $gi = Join-Path $engineOut "bin\gst-inspect-1.0.exe"
    if (Test-Path $gi) {
        $ErrorActionPreference = "Continue"
        $env:GST_PLUGIN_SYSTEM_PATH_1_0 = Join-Path $engineOut "lib\gstreamer-1.0"
        $env:GST_PLUGIN_PATH_1_0 = ""
        $env:GST_PLUGIN_SCANNER_1_0 = Join-Path $engineOut "libexec\gstreamer-1.0\gst-plugin-scanner.exe"
        $env:GST_REGISTRY_1_0 = Join-Path $Cache "test-registry.bin"
        $oldPath = $env:PATH; $env:PATH = (Join-Path $engineOut "bin") + ";$env:WINDIR\System32;$env:WINDIR"
        foreach ($el in @("h264parse", "decodebin", "avdec_h264", "avdec_aac", "d3d11videosink", "wasapisink", "audioconvert", "videoconvert")) {
            & $gi --exists $el 2>$null
            if ($LASTEXITCODE -eq 0) { Ok "GStreamer: $el" } else { Warn "GStreamer ogesi yuklenemedi: $el" }
        }
        $env:PATH = $oldPath
        Remove-Item Env:GST_PLUGIN_SYSTEM_PATH_1_0, Env:GST_PLUGIN_PATH_1_0, Env:GST_PLUGIN_SCANNER_1_0, Env:GST_REGISTRY_1_0 -ErrorAction SilentlyContinue
        $ErrorActionPreference = "Stop"
    }
}

# ------------------------------------------------------------------ 4) Bonjour
Step "4/5" "Apple Bonjour paketi (kurulumda eksikse yuklenmek uzere)"
$redist = Join-Path $Payload "redist"
if (-not (Test-Path $redist)) { New-Item -ItemType Directory -Path $redist -Force | Out-Null }
$msi = Join-Path $redist "Bonjour64.msi"
if (-not (Test-Path $msi)) {
    $bps = Join-Path $Cache "BonjourPSSetup.exe"
    if (-not (Test-Path $bps)) { Get-File "https://download.info.apple.com/Mac_OS_X/061-8098.20100603.gthyu/BonjourPSSetup.exe" $bps | Out-Null }
    if ((Test-Path $bps) -and -not (Test-Path "$MsysRoot\usr\bin\7z") -and (Test-Path "$MsysRoot\usr\bin\bash.exe")) {
        Write-Host "    7-Zip (p7zip) kuruluyor..."
        Invoke-Msys "pacman -S --noconfirm --needed p7zip" | Out-Null
    }
    if ((Test-Path $bps) -and (Test-Path "$MsysRoot\usr\bin\7z")) {
        $tmp = Join-Path $Cache "bonjour_x"; Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
        # MSYS2'deki 7z bir kabuk betigidir (.exe degil) -> bash uzerinden calistir
        Invoke-Msys "7z x -y -o'$(To-Unix $tmp)' '$(To-Unix $bps)'" | Out-Null
        $found = Get-ChildItem $tmp -Recurse -Filter "Bonjour64.msi" -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($found) { Copy-Item $found.FullName $msi -Force; Ok "Bonjour64.msi pakete eklendi" }
        else { Warn "Bonjour64.msi cikarilamadi - kurulum sirasinda internetten kurulacak" }
    } else { Warn "Bonjour paketlenemedi - kurulum sirasinda internetten kurulacak (winget / Apple)" }
} else { Ok "Bonjour64.msi mevcut" }

# Kurulum betikleri ve ikon
$scriptsDst = Join-Path $Payload "scripts"
if (-not (Test-Path $scriptsDst)) { New-Item -ItemType Directory -Path $scriptsDst -Force | Out-Null }
Copy-Item "$Root\installer\postinstall.ps1" $scriptsDst -Force
Copy-Item "$Root\installer\uninstall.ps1" $scriptsDst -Force
Copy-Item "$Root\assets\nooxair.ico" $Payload -Force

# ------------------------------------------------------------------ 5) Inno Setup
Step "5/5" "Kurulum dosyasi (Inno Setup)"
function Find-ISCC {
    $c = @()
    foreach ($base in @(${env:ProgramFiles(x86)}, $env:ProgramFiles, "$env:LOCALAPPDATA\Programs")) {
        if ($base -and (Test-Path $base)) {
            $c += Get-ChildItem $base -Directory -Filter "Inno Setup*" -ErrorAction SilentlyContinue | ForEach-Object { Join-Path $_.FullName "ISCC.exe" }
        }
    }
    $cmd = Get-Command iscc.exe -ErrorAction SilentlyContinue
    if ($cmd) { $c += $cmd.Source }
    return $c | Where-Object { Test-Path $_ } | Select-Object -First 1
}
$iscc = Find-ISCC
if (-not $iscc) {
    Write-Host "    Inno Setup bulunamadi, kuruluyor..."
    $winget = Get-Command winget.exe -ErrorAction SilentlyContinue
    if ($winget) {
        & winget install --id JRSoftware.InnoSetup -e --silent --scope user --accept-package-agreements --accept-source-agreements | Out-Null
        $iscc = Find-ISCC
    }
    if (-not $iscc) {
        $is = Join-Path $Cache "innosetup.exe"
        if (Get-File "https://jrsoftware.org/download.php/is.exe" $is) {
            Start-Process $is -ArgumentList "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/SP-", "/CURRENTUSER" -Wait
            $iscc = Find-ISCC
        }
    }
    if (-not $iscc) { Fail "Inno Setup kurulamadi. https://jrsoftware.org/isdl.php adresinden kurup tekrar deneyin." }
}
Ok "ISCC: $iscc"

if (-not (Test-Path $engineExe)) { Warn "DIKKAT: motor yok, kurulum paketi motorsuz olusturuluyor!" }
& $iscc /Q "/DPayload=$Payload" "/O$Dist" (Join-Path $Root "installer\NooXAIR.iss")
if ($LASTEXITCODE -ne 0) { Fail "Inno Setup derlemesi basarisiz" }

$setup = Join-Path $Dist "NooXAIR_Setup.exe"
$mb = [Math]::Round((Get-Item $setup).Length / 1MB, 1)
Write-Host ""
Write-Host "=====================================================" -ForegroundColor Green
Write-Host "  HAZIR: $setup  ($mb MB)" -ForegroundColor Green
Write-Host "=====================================================" -ForegroundColor Green
