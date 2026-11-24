@echo off
setlocal
chcp 65001 >nul
title NooXAIR - Setup Olusturucu
cd /d "%~dp0"

rem Kullanim:
rem   build.bat            -> her seyi derler, dist\NooXAIR_Setup.exe olusturur
rem   build.bat rebuild    -> UxPlay motorunu sifirdan yeniden derler
rem   build.bat app        -> sadece arayuz/kurulum (motor derlemesini atlar, mevcut motoru kullanir)

set ARGS=
if /i "%~1"=="rebuild" set ARGS=-RebuildEngine
if /i "%~1"=="app" set ARGS=-SkipEngine

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\build.ps1" %ARGS%
set RC=%ERRORLEVEL%

echo.
if %RC% NEQ 0 (
  echo [HATA] Derleme basarisiz oldu. Yukaridaki mesajlari kontrol edin.
) else (
  echo [TAMAM] Kurulum dosyasi: %~dp0dist\NooXAIR_Setup.exe
  explorer "%~dp0dist"
)
pause
exit /b %RC%
