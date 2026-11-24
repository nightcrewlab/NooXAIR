<#
  NooXAIR kurulum sonrasi otomasyonu (Inno Setup tarafindan yonetici olarak calistirilir)
   - Apple Bonjour: tespit, eksikse kur (paketli MSI -> winget -> Apple indirmesi), otomatik baslat + cokme kurtarma
   - Windows Guvenlik Duvari: motor + AirPlay/mDNS portlari (tum profiller), eski engelleme kurallarini temizle
   - (istege bagli) Ag profilini Ozel yap, Wi-Fi guc tasarrufunu kapat
#>
param(
    [Parameter(Mandatory = $true)][string]$AppDir,
    [switch]$PrivateNetwork,
    [switch]$WifiPerformance
)

$ErrorActionPreference = "Continue"
$ProgressPreference = "SilentlyContinue"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$LogDir = Join-Path $env:ProgramData "NooXAIR"
if (-not (Test-Path $LogDir)) { New-Item -ItemType Directory -Path $LogDir -Force | Out-Null }
$Log = Join-Path $LogDir "install.log"
function L($m) { $line = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')  $m"; Add-Content -Path $Log -Value $line -Encoding UTF8; Write-Host $line }

L "=== NooXAIR kurulum sonrasi yapilandirma basladi ($AppDir) ==="
$EngineExe = Join-Path $AppDir "engine\bin\uxplay.exe"
$AppExe    = Join-Path $AppDir "NooXAIR.exe"

# ------------------------------------------------------------------ 1) Bonjour
function Get-Bonjour { Get-Service -Name "Bonjour Service" -ErrorAction SilentlyContinue }

$svc = Get-Bonjour
if ($null -eq $svc) {
    L "Bonjour bulunamadi -> kuruluyor"
    $msi = Join-Path $AppDir "redist\Bonjour64.msi"
    if (Test-Path $msi) {
        $p = Start-Process msiexec.exe -ArgumentList "/i", "`"$msi`"", "/qn", "/norestart" -Wait -PassThru
        L "Paketli Bonjour64.msi kuruldu (cikis kodu $($p.ExitCode))"
    }
    if ($null -eq (Get-Bonjour)) {
        $winget = Get-Command winget.exe -ErrorAction SilentlyContinue
        if ($winget) {
            L "winget ile Apple.BonjourPrintServices deneniyor"
            & winget install --id Apple.BonjourPrintServices -e --silent --scope machine --accept-package-agreements --accept-source-agreements | Out-Null
        }
    }
    if ($null -eq (Get-Bonjour)) {
        L "Apple sunucusundan Bonjour Print Services indiriliyor"
        $tmp = Join-Path $env:TEMP "BonjourPSSetup.exe"
        try {
            Invoke-WebRequest -Uri "https://download.info.apple.com/Mac_OS_X/061-8098.20100603.gthyu/BonjourPSSetup.exe" -OutFile $tmp -UseBasicParsing -TimeoutSec 300
            Start-Process $tmp -Wait
        } catch { L "Bonjour indirilemedi: $($_.Exception.Message)" }
    }
    $svc = Get-Bonjour
}

if ($null -ne $svc) {
    try {
        Set-Service -Name "Bonjour Service" -StartupType Automatic
        & sc.exe failure "Bonjour Service" reset= 86400 actions= restart/3000/restart/5000/restart/10000 | Out-Null
        if ((Get-Bonjour).Status -ne "Running") { Start-Service -Name "Bonjour Service" }
        L "Bonjour: otomatik baslatma + cokme kurtarma ayarlandi, durum=$((Get-Bonjour).Status)"
    } catch { L "Bonjour servis ayari hatasi: $($_.Exception.Message)" }
} else {
    L "UYARI: Bonjour kurulamadi. NooXAIR motoru kendi mDNS yayincisini icerdigi icin yine de calisir."
}

# ------------------------------------------------------------------ 2) Guvenlik duvari
L "Guvenlik duvari kurallari ayarlaniyor"
Get-NetFirewallRule -DisplayName "NooXAIR*" -ErrorAction SilentlyContinue | Remove-NetFirewallRule -ErrorAction SilentlyContinue

# Kullanici daha once Windows uyarisinda "Iptal" dediyse olusan ENGELLEME kurallarini kaldir
foreach ($prog in @($EngineExe, $AppExe)) {
    try {
        Get-NetFirewallApplicationFilter -Program $prog -ErrorAction SilentlyContinue |
            Get-NetFirewallRule -ErrorAction SilentlyContinue | Where-Object { $_.Action -eq "Block" } |
            ForEach-Object { L "Engelleme kurali kaldirildi: $($_.DisplayName)"; Remove-NetFirewallRule -Name $_.Name }
    } catch {}
}

function Add-Rule($name, $params) {
    try { New-NetFirewallRule -DisplayName $name -Profile Any -Action Allow -Enabled True @params -ErrorAction Stop | Out-Null; L "  + $name" }
    catch { L "  ! $name : $($_.Exception.Message)" }
}
Add-Rule "NooXAIR Motor (Gelen)" @{ Direction = "Inbound";  Program = $EngineExe }
Add-Rule "NooXAIR Motor (Giden)" @{ Direction = "Outbound"; Program = $EngineExe }
Add-Rule "NooXAIR mDNS UDP 5353" @{ Direction = "Inbound";  Protocol = "UDP"; LocalPort = @("5353") }
$mdns = Join-Path $env:ProgramFiles "Bonjour\mDNSResponder.exe"
if (Test-Path $mdns) { Add-Rule "NooXAIR Bonjour mDNSResponder" @{ Direction = "Inbound"; Program = $mdns } }

# ------------------------------------------------------------------ 3) Ag profili (istege bagli)
if ($PrivateNetwork) {
    Get-NetConnectionProfile -ErrorAction SilentlyContinue | Where-Object { $_.NetworkCategory -eq "Public" } | ForEach-Object {
        try { Set-NetConnectionProfile -InterfaceIndex $_.InterfaceIndex -NetworkCategory Private -ErrorAction Stop; L "Ag profili Ozel yapildi: $($_.Name)" }
        catch { L "Ag profili degistirilemedi ($($_.Name)): $($_.Exception.Message)" }
    }
}

# ------------------------------------------------------------------ 4) Wi-Fi performans (istege bagli)
if ($WifiPerformance) {
    # Kablosuz Bagdastirici Ayarlari > Guc Tasarrufu Modu = Maksimum Performans (prizde)
    & powercfg /setacvalueindex SCHEME_CURRENT 19cbb8fa-5279-450e-9fac-8a3d5fedd0c1 12bbebe6-58d6-4636-95bb-3217ef867c1a 0 | Out-Null
    & powercfg /setactive SCHEME_CURRENT | Out-Null
    # Wi-Fi bagdastiricilarinda "guc tasarrufu icin kapat" secenegini devre disi birak
    Get-NetAdapter -Physical -ErrorAction SilentlyContinue | Where-Object { $_.MediaType -match "802.11|Native 802.11" -or $_.InterfaceDescription -match "Wi-?Fi|Wireless|WLAN" } | ForEach-Object {
        try { Disable-NetAdapterPowerManagement -Name $_.Name -NoRestart -ErrorAction Stop; L "Wi-Fi guc yonetimi kapatildi: $($_.Name)" } catch {}
    }
    L "Wi-Fi guc tasarrufu: maksimum performans"
}

L "=== Yapilandirma tamamlandi ==="
exit 0
