# NooXAIR kaldirma temizligi (Bonjour sistemde birakilir - baska uygulamalar da kullanabilir)
$ErrorActionPreference = "SilentlyContinue"
Get-Process -Name "NooXAIR", "uxplay" | Stop-Process -Force
Get-NetFirewallRule -DisplayName "NooXAIR*" | Remove-NetFirewallRule
Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "NooXAIR"
exit 0
