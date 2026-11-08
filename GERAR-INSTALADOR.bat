@echo off
chcp 65001 >nul
cd /d "%~dp0"

echo Gerando o instalador do LoL Idle Desktop...
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "installer\build-installer.ps1"
if errorlevel 1 (
  echo.
  echo Falha ao gerar o instalador.
  pause
  exit /b 1
)

echo.
echo Instalador pronto em:
echo artifacts\installer\LoLIdleDesktop-Setup-*-x64.exe
pause
